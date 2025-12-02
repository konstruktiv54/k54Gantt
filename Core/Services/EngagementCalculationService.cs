using Core.Models;

namespace Core.Services;

/// <summary>
/// Сервис расчёта вовлечённости и загрузки ресурсов.
/// Отвечает за вычисление AllocationPercent и DayState.
/// </summary>
public class EngagementCalculationService
{
    private readonly ResourceService _resourceService;
    private ProjectManager? _projectManager;

    /// <summary>
    /// Порог завершённости, при котором задача считается выполненной (100%).
    /// </summary>
    private const float CompletionThreshold = 0.9999f;

    /// <summary>
    /// Создаёт сервис расчёта вовлечённости.
    /// </summary>
    /// <param name="resourceService">Сервис управления ресурсами.</param>
    public EngagementCalculationService(ResourceService resourceService)
    {
        _resourceService = resourceService ?? throw new ArgumentNullException(nameof(resourceService));
    }

    /// <summary>
    /// Создаёт сервис расчёта вовлечённости с указанным ProjectManager.
    /// </summary>
    /// <param name="resourceService">Сервис управления ресурсами.</param>
    /// <param name="projectManager">Менеджер проекта (для доступа к задачам).</param>
    public EngagementCalculationService(
        ResourceService resourceService,
        ProjectManager projectManager)
        : this(resourceService)
    {
        _projectManager = projectManager;
    }

    /// <summary>
    /// Устанавливает или получает менеджер проекта.
    /// Необходим для расчётов, связанных с задачами.
    /// </summary>
    public ProjectManager? ProjectManager
    {
        get => _projectManager;
        set => _projectManager = value;
    }

    /// <summary>
    /// Проверяет, инициализирован ли сервис (установлен ProjectManager).
    /// </summary>
    public bool IsInitialized => _projectManager != null;

    /// <summary>
    /// Возвращает коэффициент нагрузки для роли.
    /// </summary>
    /// <param name="role">Роль ресурса.</param>
    /// <returns>Коэффициент (0.0 - 1.0).</returns>
    public double GetRoleCoefficient(ResourceRole role)
    {
        return role.GetCoefficient();
    }

    /// <summary>
    /// Рассчитывает процент загрузки ресурса на конкретный день.
    /// Формула: Σ(Assignment.Workload × RoleCoefficient)
    /// Примечание: Завершённые задачи (Complete = 100%) не учитываются.
    /// </summary>
    /// <param name="resourceId">ID ресурса.</param>
    /// <param name="day">День (смещение от начала проекта).</param>
    /// <returns>Процент загрузки (может быть > 100 при перегрузке).</returns>
    public int CalculateAllocationPercent(Guid resourceId, TimeSpan day)
    {
        var resource = _resourceService.GetResourceById(resourceId);
        if (resource == null)
            return 0;

        if (_projectManager == null)
            return 0;

        var coefficient = GetRoleCoefficient(resource.Role);
        var assignments = GetAssignmentsForDay(resourceId, day);

        double totalAllocation = 0;
        foreach (var assignment in assignments)
        {
            totalAllocation += assignment.Workload * coefficient;
        }

        return (int)Math.Round(totalAllocation);
    }

    /// <summary>
    /// Определяет состояние дня для ресурса.
    /// </summary>
    /// <param name="resourceId">ID ресурса.</param>
    /// <param name="day">День.</param>
    /// <returns>Состояние дня.</returns>
    public DayState CalculateDayState(Guid resourceId, TimeSpan day)
    {
        // ═══ ПРОВЕРКА ВЫХОДНЫХ (наивысший приоритет) ═══
        if (IsWeekend(day))
        {
            return DayState.Weekend;
        }
        
        // 1. Проверяем участие в проекте
        var interval = _resourceService.GetParticipationIntervalForDay(resourceId, day);
        bool inParticipation = interval != null;
        int maxWorkload = interval?.MaxWorkload ?? 0;

        // 2. Проверяем отсутствие
        bool inAbsence = _resourceService.IsResourceAbsent(resourceId, day);

        // 3. Получаем назначения и загрузку (завершённые задачи уже отфильтрованы)
        var assignments = GetAssignmentsForDay(resourceId, day);
        int allocationPercent = CalculateAllocationPercent(resourceId, day);
        bool hasAssignments = assignments.Count > 0;

        // 4. Определяем состояние по приоритетам

        // Overbooked: перегрузка ИЛИ (назначение + отсутствие) ИЛИ (назначение + неучастие)
        if (allocationPercent > maxWorkload && inParticipation)
            return DayState.Overbooked;

        if (hasAssignments && inAbsence)
            return DayState.Overbooked;

        if (hasAssignments && !inParticipation)
            return DayState.Overbooked;

        // Не участвует (без назначений)
        if (!inParticipation)
            return DayState.NotParticipating;

        // Отсутствует (без назначений)
        if (inAbsence)
            return DayState.Absence;

        // Assigned: загрузка = максимум
        if (allocationPercent >= maxWorkload && maxWorkload > 0)
            return DayState.Assigned;

        // PartialAssigned: частичная загрузка
        if (allocationPercent > 0)
            return DayState.PartialAssigned;

        // Free: нет загрузки
        return DayState.Free;
    }

    /// <summary>
    /// Получает полный статус дня для ресурса.
    /// </summary>
    /// <param name="resourceId">ID ресурса.</param>
    /// <param name="day">День.</param>
    /// <returns>Статус дня со всей информацией.</returns>
    public DayStatus GetDayStatus(Guid resourceId, TimeSpan day)
    {
        // ═══ ПРОВЕРКА ВЫХОДНЫХ (наивысший приоритет) ═══
        if (_projectManager != null && IsWeekend(day))
        {
            return DayStatus.Weekend(day, resourceId);
        }
        
        var resource = _resourceService.GetResourceById(resourceId);
        if (resource == null)
            return DayStatus.NotParticipating(day, resourceId);

        // Участие
        var interval = _resourceService.GetParticipationIntervalForDay(resourceId, day);
        bool inParticipation = interval != null;
        int maxWorkload = interval?.MaxWorkload ?? 0;

        // Отсутствие
        var absence = _resourceService.GetAbsenceForDay(resourceId, day);
        bool inAbsence = absence != null;
        string? absenceReason = absence?.Reason;

        // Назначения и загрузка (завершённые задачи уже отфильтрованы)
        var assignments = GetAssignmentsForDay(resourceId, day);
        int allocationPercent = CalculateAllocationPercentInternal(resource.Role, assignments);

        // Состояние
        var state = CalculateDayStateInternal(
            inParticipation, maxWorkload, inAbsence, 
            assignments.Count > 0, allocationPercent);

        return new DayStatus(
            day: day,
            resourceId: resourceId,
            inParticipation: inParticipation,
            maxWorkload: maxWorkload,
            inAbsence: inAbsence,
            absenceReason: absenceReason,
            assignments: assignments,
            allocationPercent: allocationPercent,
            state: state);
    }

    /// <summary>
    /// Получает статусы для диапазона дней.
    /// </summary>
    /// <param name="resourceId">ID ресурса.</param>
    /// <param name="startDay">Начальный день (включительно).</param>
    /// <param name="endDay">Конечный день (не включительно).</param>
    /// <returns>Список статусов по дням.</returns>
    public List<DayStatus> GetDayStatusRange(Guid resourceId, TimeSpan startDay, TimeSpan endDay)
    {
        var result = new List<DayStatus>();
        
        var currentDay = startDay;
        while (currentDay < endDay)
        {
            result.Add(GetDayStatus(resourceId, currentDay));
            currentDay += TimeSpan.FromDays(1);
        }

        return result;
    }

    /// <summary>
    /// Получает статусы для всех ресурсов на диапазон дней.
    /// </summary>
    /// <param name="startDay">Начальный день.</param>
    /// <param name="endDay">Конечный день.</param>
    /// <returns>Словарь: ResourceId → список статусов.</returns>
    public Dictionary<Guid, List<DayStatus>> GetAllResourcesStatusRange(TimeSpan startDay, TimeSpan endDay)
    {
        var result = new Dictionary<Guid, List<DayStatus>>();

        foreach (var resource in _resourceService.GetAllResources())
        {
            result[resource.Id] = GetDayStatusRange(resource.Id, startDay, endDay);
        }

        return result;
    }

    /// <summary>
    /// Получает назначения ресурса на конкретный день.
    /// Учитывает только активные (незавершённые) задачи.
    /// </summary>
    /// <param name="resourceId">ID ресурса.</param>
    /// <param name="day">День.</param>
    /// <returns>Список назначений на незавершённые задачи.</returns>
    public List<ResourceAssignment> GetAssignmentsForDay(Guid resourceId, TimeSpan day)
    {
        if (_projectManager == null)
            return new List<ResourceAssignment>();

        var allAssignments = _resourceService.GetAssignmentsByResource(resourceId);
        var result = new List<ResourceAssignment>();

        foreach (var assignment in allAssignments)
        {
            var task = _projectManager.GetTaskById(assignment.TaskId);
            if (task == null)
                continue;

            // Пропускаем завершённые задачи (Complete >= 100%)
            if (task.Complete >= CompletionThreshold)
                continue;

            // Проверяем, попадает ли день в период задачи [Start, Start + Duration)
            if (day >= task.Start && day < task.Start + task.Duration)
            {
                result.Add(assignment);
            }
        }

        return result;
    }

    /// <summary>
    /// Получает все назначения на день, включая завершённые задачи.
    /// Используется для отображения истории или отчётов.
    /// </summary>
    /// <param name="resourceId">ID ресурса.</param>
    /// <param name="day">День.</param>
    /// <returns>Список всех назначений (включая завершённые).</returns>
    public List<ResourceAssignment> GetAllAssignmentsForDay(Guid resourceId, TimeSpan day)
    {
        if (_projectManager == null)
            return new List<ResourceAssignment>();

        var allAssignments = _resourceService.GetAssignmentsByResource(resourceId);
        var result = new List<ResourceAssignment>();

        foreach (var assignment in allAssignments)
        {
            var task = _projectManager.GetTaskById(assignment.TaskId);
            if (task == null)
                continue;

            // Проверяем, попадает ли день в период задачи
            if (day >= task.Start && day < task.Start + task.Duration)
            {
                result.Add(assignment);
            }
        }

        return result;
    }

    /// <summary>
    /// Проверяет, будет ли ресурс перегружен при добавлении назначения.
    /// </summary>
    /// <param name="resourceId">ID ресурса.</param>
    /// <param name="taskStart">Начало задачи.</param>
    /// <param name="taskDuration">Длительность задачи.</param>
    /// <param name="workload">Нагрузка назначения (%).</param>
    /// <returns>True, если возникнет перегрузка.</returns>
    public bool WillBeOverbooked(Guid resourceId, TimeSpan taskStart, TimeSpan taskDuration, int workload)
    {
        var resource = _resourceService.GetResourceById(resourceId);
        if (resource == null)
            return false;

        var coefficient = GetRoleCoefficient(resource.Role);
        var additionalLoad = (int)Math.Round(workload * coefficient);

        var endDay = taskStart + taskDuration;
        var currentDay = taskStart;

        while (currentDay < endDay)
        {
            var interval = _resourceService.GetParticipationIntervalForDay(resourceId, currentDay);
            if (interval == null)
            {
                // Назначение вне периода участия = перегрузка
                return true;
            }

            var currentAllocation = CalculateAllocationPercent(resourceId, currentDay);
            if (currentAllocation + additionalLoad > interval.MaxWorkload)
            {
                return true;
            }

            currentDay += TimeSpan.FromDays(1);
        }

        return false;
    }

    /// <summary>
    /// Находит дни перегрузки для ресурса в диапазоне.
    /// </summary>
    /// <param name="resourceId">ID ресурса.</param>
    /// <param name="startDay">Начало диапазона.</param>
    /// <param name="endDay">Конец диапазона.</param>
    /// <returns>Список дней с перегрузкой.</returns>
    public List<TimeSpan> FindOverbookedDays(Guid resourceId, TimeSpan startDay, TimeSpan endDay)
    {
        var result = new List<TimeSpan>();
        var currentDay = startDay;

        while (currentDay < endDay)
        {
            if (CalculateDayState(resourceId, currentDay) == DayState.Overbooked)
            {
                result.Add(currentDay);
            }
            currentDay += TimeSpan.FromDays(1);
        }

        return result;
    }

    #region Private Methods
    
    /// <summary>
    /// Проверяет, является ли день выходным (суббота или воскресенье).
    /// </summary>
    /// <param name="day">День (смещение от начала проекта).</param>
    /// <returns>True, если суббота или воскресенье.</returns>
    private bool IsWeekend(TimeSpan day)
    {
        if (_projectManager == null)
            return false;

        var actualDate = _projectManager.Start.AddDays(day.Days);
        return actualDate.DayOfWeek == DayOfWeek.Saturday 
               || actualDate.DayOfWeek == DayOfWeek.Sunday;
    }

    private int CalculateAllocationPercentInternal(ResourceRole role, IReadOnlyList<ResourceAssignment> assignments)
    {
        var coefficient = GetRoleCoefficient(role);
        double total = 0;
        
        foreach (var assignment in assignments)
        {
            total += assignment.Workload * coefficient;
        }

        return (int)Math.Round(total);
    }

    private DayState CalculateDayStateInternal(
        bool inParticipation,
        int maxWorkload,
        bool inAbsence,
        bool hasAssignments,
        int allocationPercent)
    {
        // Overbooked conditions
        if (allocationPercent > maxWorkload && inParticipation)
            return DayState.Overbooked;

        if (hasAssignments && inAbsence)
            return DayState.Overbooked;

        if (hasAssignments && !inParticipation)
            return DayState.Overbooked;

        if (!inParticipation)
            return DayState.NotParticipating;

        if (inAbsence)
            return DayState.Absence;

        if (allocationPercent >= maxWorkload && maxWorkload > 0)
            return DayState.Assigned;

        if (allocationPercent > 0)
            return DayState.PartialAssigned;

        return DayState.Free;
    }

    #endregion
}