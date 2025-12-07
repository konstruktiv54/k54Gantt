using Core.Models;
using Task = Core.Interfaces.Task;

namespace Core.Services;

/// <summary>
/// Калькулятор рабочих дней для задач.
/// Учитывает выходные, праздники и отсутствия назначенных ресурсов.
/// </summary>
public class WorkingDaysCalculator
{
    private readonly ResourceService _resourceService;
    private readonly ProductionCalendarService _calendarService;

    /// <summary>
    /// Событие, возникающее при необходимости пересчёта рабочих дней.
    /// </summary>
    public event EventHandler? RecalculationNeeded;

    public WorkingDaysCalculator(
        ResourceService resourceService,
        ProductionCalendarService calendarService)
    {
        _resourceService = resourceService ?? throw new ArgumentNullException(nameof(resourceService));
        _calendarService = calendarService ?? throw new ArgumentNullException(nameof(calendarService));

        // Подписываемся на изменения, требующие пересчёта
        _calendarService.HolidaysChanged += OnDataChanged;
        _resourceService.ResourcesChanged += OnDataChanged;
    }

    private void OnDataChanged(object? sender, EventArgs e)
    {
        RecalculationNeeded?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Рассчитывает количество рабочих дней для задачи.
    /// Учитывает выходные, праздники и отсутствия назначенных ресурсов.
    /// </summary>
    /// <param name="task">Задача для расчёта.</param>
    /// <param name="projectStart">Дата начала проекта.</param>
    /// <returns>Количество рабочих дней.</returns>
    public int CalculateWorkingDays(Task task, DateTime projectStart)
    {
        if (task == null)
            return 0;

        // Получаем назначенные ресурсы
        var assignedResourceIds = _resourceService
            .GetResourcesForTask(task.Id)
            .Select(r => r.Id)
            .ToList();

        return CalculateWorkingDays(
            task.Start,
            task.Duration,
            projectStart,
            assignedResourceIds);
    }

    /// <summary>
    /// Рассчитывает количество рабочих дней для диапазона.
    /// </summary>
    /// <param name="start">Начало периода (относительно начала проекта).</param>
    /// <param name="duration">Длительность периода.</param>
    /// <param name="projectStart">Дата начала проекта.</param>
    /// <param name="resourceIds">Идентификаторы назначенных ресурсов (опционально).</param>
    /// <returns>Количество рабочих дней.</returns>
    public int CalculateWorkingDays(
        TimeSpan start,
        TimeSpan duration,
        DateTime projectStart,
        IEnumerable<Guid>? resourceIds = null)
    {
        var resourceIdsList = resourceIds?.ToList() ?? new List<Guid>();
        var calendarDays = (int)Math.Round(duration.TotalDays);
        
        if (calendarDays <= 0)
            return 0;

        int workingDays = 0;
        var startDay = (int)Math.Round(start.TotalDays);

        for (int dayOffset = 0; dayOffset < calendarDays; dayOffset++)
        {
            var currentDay = TimeSpan.FromDays(startDay + dayOffset);
            
            if (!IsNonWorkingDay(currentDay, projectStart, resourceIdsList))
            {
                workingDays++;
            }
        }

        return workingDays;
    }

    /// <summary>
    /// Проверяет, является ли день нерабочим.
    /// </summary>
    /// <param name="day">День относительно начала проекта.</param>
    /// <param name="projectStart">Дата начала проекта.</param>
    /// <param name="resourceIds">Идентификаторы ресурсов для проверки отсутствий.</param>
    /// <returns>True, если день нерабочий.</returns>
    public bool IsNonWorkingDay(
        TimeSpan day,
        DateTime projectStart,
        IEnumerable<Guid>? resourceIds = null)
    {
        var date = projectStart.Date.AddDays((int)Math.Round(day.TotalDays));

        // 1. Проверяем выходные
        if (IsWeekend(date))
            return true;

        // 2. Проверяем праздники
        if (_calendarService.IsHoliday(day))
            return true;

        // 3. Проверяем отсутствия ресурсов (если есть назначенные)
        var resourceIdsList = resourceIds?.ToList();
        if (resourceIdsList != null && resourceIdsList.Count > 0)
        {
            // День нерабочий, если ВСЕ назначенные ресурсы отсутствуют
            if (AreAllResourcesAbsent(day, resourceIdsList))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Проверяет, является ли дата выходным днём (суббота или воскресенье).
    /// </summary>
    /// <param name="date">Дата для проверки.</param>
    /// <returns>True, если выходной.</returns>
    public static bool IsWeekend(DateTime date)
    {
        return date.DayOfWeek == DayOfWeek.Saturday || 
               date.DayOfWeek == DayOfWeek.Sunday;
    }

    /// <summary>
    /// Проверяет, отсутствуют ли все указанные ресурсы в данный день.
    /// </summary>
    /// <param name="day">День для проверки.</param>
    /// <param name="resourceIds">Идентификаторы ресурсов.</param>
    /// <returns>True, если все ресурсы отсутствуют.</returns>
    private bool AreAllResourcesAbsent(TimeSpan day, List<Guid> resourceIds)
    {
        if (resourceIds.Count == 0)
            return false;

        foreach (var resourceId in resourceIds)
        {
            var absences = _resourceService.GetAbsencesForResource(resourceId);
            
            // Проверяем, есть ли у ресурса отсутствие на этот день
            bool isAbsent = absences.Any(a => 
                day >= a.Start && day <= a.End);

            // Если хотя бы один ресурс НЕ отсутствует — день рабочий
            if (!isAbsent)
                return false;
        }

        // Все ресурсы отсутствуют
        return true;
    }

    /// <summary>
    /// Получает детальную информацию о нерабочих днях в диапазоне задачи.
    /// </summary>
    /// <param name="task">Задача для анализа.</param>
    /// <param name="projectStart">Дата начала проекта.</param>
    /// <returns>Детализация нерабочих дней.</returns>
    public NonWorkingDaysBreakdown GetBreakdown(Task task, DateTime projectStart)
    {
        var result = new NonWorkingDaysBreakdown();
        
        if (task == null)
            return result;

        var calendarDays = (int)Math.Round(task.Duration.TotalDays);
        var startDay = (int)Math.Round(task.Start.TotalDays);
        
        var resourceIds = _resourceService
            .GetResourcesForTask(task.Id)
            .Select(r => r.Id)
            .ToList();

        for (int dayOffset = 0; dayOffset < calendarDays; dayOffset++)
        {
            var currentDay = TimeSpan.FromDays(startDay + dayOffset);
            var date = projectStart.Date.AddDays(startDay + dayOffset);

            if (IsWeekend(date))
            {
                result.WeekendDays++;
            }
            else if (_calendarService.IsHoliday(currentDay))
            {
                result.HolidayDays++;
            }
            else if (resourceIds.Count > 0 && AreAllResourcesAbsent(currentDay, resourceIds))
            {
                result.AbsenceDays++;
            }
            else
            {
                result.WorkingDays++;
            }
        }

        result.CalendarDays = calendarDays;
        return result;
    }
}

/// <summary>
/// Детализация нерабочих дней задачи.
/// </summary>
public class NonWorkingDaysBreakdown
{
    /// <summary>
    /// Общее количество календарных дней.
    /// </summary>
    public int CalendarDays { get; set; }

    /// <summary>
    /// Количество рабочих дней.
    /// </summary>
    public int WorkingDays { get; set; }

    /// <summary>
    /// Количество выходных дней (сб/вс).
    /// </summary>
    public int WeekendDays { get; set; }

    /// <summary>
    /// Количество праздничных дней.
    /// </summary>
    public int HolidayDays { get; set; }

    /// <summary>
    /// Количество дней, когда все ресурсы отсутствуют.
    /// </summary>
    public int AbsenceDays { get; set; }

    /// <summary>
    /// Общее количество нерабочих дней.
    /// </summary>
    public int TotalNonWorkingDays => WeekendDays + HolidayDays + AbsenceDays;
}