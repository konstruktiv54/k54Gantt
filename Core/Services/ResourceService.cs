using Core.Models;
using Task = Core.Interfaces.Task;

namespace Core.Services;

/// <summary>
/// Сервис для управления ресурсами проекта, их назначениями на задачи,
/// интервалами участия и отсутствиями.
/// Обеспечивает CRUD операции и управление связями ресурс-задача.
/// </summary>
public class ResourceService
{
    #region Fields

    private readonly List<Resource> _resources = new();
    private readonly List<ResourceAssignment> _assignments = new();
    private readonly List<ParticipationInterval> _participationIntervals = new();
    private readonly List<Absence> _absences = new();

    #endregion

    #region Events

    /// <summary>
    /// Событие, возникающее при изменении списка ресурсов.
    /// </summary>
    public event EventHandler? ResourcesChanged;

    /// <summary>
    /// Событие, возникающее при изменении назначений.
    /// </summary>
    public event EventHandler? AssignmentsChanged;

    /// <summary>
    /// Событие, возникающее при изменении интервалов участия.
    /// </summary>
    public event EventHandler? ParticipationIntervalsChanged;

    /// <summary>
    /// Событие, возникающее при изменении отсутствий.
    /// </summary>
    public event EventHandler? AbsencesChanged;

    #endregion

    #region Properties

    /// <summary>
    /// Возвращает коллекцию всех ресурсов (только для чтения).
    /// </summary>
    public IReadOnlyList<Resource> Resources => _resources.AsReadOnly();

    /// <summary>
    /// Возвращает коллекцию всех назначений (только для чтения).
    /// </summary>
    public IReadOnlyList<ResourceAssignment> Assignments => _assignments.AsReadOnly();

    /// <summary>
    /// Возвращает коллекцию всех интервалов участия (только для чтения).
    /// </summary>
    public IReadOnlyList<ParticipationInterval> ParticipationIntervals => _participationIntervals.AsReadOnly();

    /// <summary>
    /// Возвращает коллекцию всех отсутствий (только для чтения).
    /// </summary>
    public IReadOnlyList<Absence> Absences => _absences.AsReadOnly();

    /// <summary>
    /// Количество ресурсов.
    /// </summary>
    public int ResourceCount => _resources.Count;

    /// <summary>
    /// Количество назначений.
    /// </summary>
    public int AssignmentCount => _assignments.Count;

    #endregion

    #region Resource CRUD Operations

    /// <summary>
    /// Добавляет новый ресурс.
    /// Автоматически создаёт дефолтный ParticipationInterval.
    /// </summary>
    public void AddResource(Resource resource)
    {
        if (resource == null)
            throw new ArgumentNullException(nameof(resource));

        if (_resources.Any(r => r.Id == resource.Id))
            throw new InvalidOperationException($"Ресурс с ID {resource.Id} уже существует.");

        _resources.Add(resource);

        // Создаём дефолтный интервал участия
        var defaultInterval = ParticipationInterval.CreateDefault(resource.Id);
        
        // Для Constructor MaxWorkload всегда 100
        if (resource.Role == ResourceRole.Constructor)
        {
            defaultInterval.MaxWorkload = 100;
        }

        _participationIntervals.Add(defaultInterval);

        OnResourcesChanged();
        OnParticipationIntervalsChanged();
    }

    /// <summary>
    /// Получает ресурс по идентификатору.
    /// </summary>
    public Resource? GetResource(Guid resourceId)
    {
        return _resources.FirstOrDefault(r => r.Id == resourceId);
    }

    /// <summary>
    /// Получает ресурс по идентификатору (алиас для GetResource).
    /// </summary>
    public Resource? GetResourceById(Guid resourceId)
    {
        return GetResource(resourceId);
    }

    /// <summary>
    /// Получает все ресурсы.
    /// </summary>
    public IEnumerable<Resource> GetAllResources()
    {
        return _resources;
    }

    /// <summary>
    /// Получает назначение ресурса на задачу.
    /// </summary>
    public ResourceAssignment? GetAssignment(Guid taskId, Guid resourceId)
    {
        return _assignments.FirstOrDefault(a => a.Matches(taskId, resourceId));
    }

    /// <summary>
    /// Создаёт и добавляет новый ресурс с указанным именем.
    /// </summary>
    public Resource CreateResource(string name, ResourceRole role = ResourceRole.Constructor)
    {
        var resource = new Resource
        {
            Name = name,
            Role = role
        };
        resource.GenerateInitialsFromName();
        AddResource(resource);
        return resource;
    }
 
    /// <summary>
    /// Обновляет существующий ресурс.
    /// </summary>
    public void UpdateResource(Resource resource)
    {
        if (resource == null)
            throw new ArgumentNullException(nameof(resource));

        var index = _resources.FindIndex(r => r.Id == resource.Id);
        if (index < 0)
            throw new InvalidOperationException($"Ресурс с ID {resource.Id} не найден.");

        // Если роль изменилась на Constructor, проверяем MaxWorkload в интервалах
        var oldResource = _resources[index];
        if (oldResource.Role != ResourceRole.Constructor && resource.Role == ResourceRole.Constructor)
        {
            // Для Constructor MaxWorkload всегда = 100
            foreach (var interval in _participationIntervals.Where(i => i.ResourceId == resource.Id))
            {
                interval.MaxWorkload = 100;
            }
            OnParticipationIntervalsChanged();
        }

        _resources[index] = resource;
        OnResourcesChanged();
    }

    /// <summary>
    /// Удаляет ресурс по идентификатору.
    /// Также удаляет все назначения, интервалы участия и отсутствия этого ресурса.
    /// </summary>
    public bool RemoveResource(Guid resourceId)
    {
        var resource = _resources.FirstOrDefault(r => r.Id == resourceId);
        if (resource == null)
            return false;

        // Удаляем все назначения этого ресурса
        var assignmentsToRemove = _assignments.Where(a => a.ResourceId == resourceId).ToList();
        foreach (var assignment in assignmentsToRemove)
        {
            _assignments.Remove(assignment);
        }

        // Удаляем все интервалы участия
        var intervalsToRemove = _participationIntervals.Where(i => i.ResourceId == resourceId).ToList();
        foreach (var interval in intervalsToRemove)
        {
            _participationIntervals.Remove(interval);
        }

        // Удаляем все отсутствия
        var absencesToRemove = _absences.Where(a => a.ResourceId == resourceId).ToList();
        foreach (var absence in absencesToRemove)
        {
            _absences.Remove(absence);
        }

        _resources.Remove(resource);

        if (assignmentsToRemove.Count > 0)
            OnAssignmentsChanged();

        if (intervalsToRemove.Count > 0)
            OnParticipationIntervalsChanged();

        if (absencesToRemove.Count > 0)
            OnAbsencesChanged();

        OnResourcesChanged();
        return true;
    }

    /// <summary>
    /// Удаляет ресурс.
    /// </summary>
    public bool RemoveResource(Resource resource)
    {
        if (resource == null) return false;
        return RemoveResource(resource.Id);
    }

    /// <summary>
    /// Получает ресурс по имени (первое совпадение).
    /// </summary>
    public Resource? GetResourceByName(string name)
    {
        return _resources.FirstOrDefault(r =>
            string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Очищает все ресурсы, назначения, интервалы и отсутствия.
    /// </summary>
    public void Clear()
    {
        _resources.Clear();
        _assignments.Clear();
        _participationIntervals.Clear();
        _absences.Clear();

        OnResourcesChanged();
        OnAssignmentsChanged();
        OnParticipationIntervalsChanged();
        OnAbsencesChanged();
    }

    #endregion

    #region Assignment Operations

    /// <summary>
    /// Назначает ресурс на задачу.
    /// </summary>
    public ResourceAssignment AssignResource(Guid taskId, Guid resourceId, int workload = 100)
    {
        // Проверяем, существует ли ресурс
        var resource = GetResource(resourceId);
        if (resource == null)
            throw new InvalidOperationException($"Ресурс с ID {resourceId} не найден.");

        // Проверяем, нет ли уже такого назначения
        if (IsAssigned(taskId, resourceId))
            return _assignments.First(a => a.Matches(taskId, resourceId));

        var assignment = new ResourceAssignment
        {
            TaskId = taskId,
            ResourceId = resourceId,
            Workload = workload
        };

        _assignments.Add(assignment);
        OnAssignmentsChanged();
        return assignment;
    }

    /// <summary>
    /// Назначает ресурс на задачу (перегрузка с объектами).
    /// </summary>
    public ResourceAssignment AssignResource(Task task, Resource resource, int workload = 100)
    {
        if (task == null) throw new ArgumentNullException(nameof(task));
        if (resource == null) throw new ArgumentNullException(nameof(resource));
        return AssignResource(task.Id, resource.Id, workload);
    }

    /// <summary>
    /// Снимает назначение ресурса с задачи.
    /// </summary>
    public bool UnassignResource(Guid taskId, Guid resourceId)
    {
        var assignment = _assignments.FirstOrDefault(a => a.Matches(taskId, resourceId));
        if (assignment == null)
            return false;

        _assignments.Remove(assignment);
        OnAssignmentsChanged();
        return true;
    }

    /// <summary>
    /// Снимает назначение ресурса с задачи (перегрузка с объектами).
    /// </summary>
    public bool UnassignResource(Task task, Resource resource)
    {
        if (task == null || resource == null) return false;
        return UnassignResource(task.Id, resource.Id);
    }

    /// <summary>
    /// Снимает все назначения с задачи.
    /// </summary>
    public int UnassignAllFromTask(Guid taskId)
    {
        var toRemove = _assignments.Where(a => a.TaskId == taskId).ToList();
        foreach (var assignment in toRemove)
        {
            _assignments.Remove(assignment);
        }

        if (toRemove.Count > 0)
            OnAssignmentsChanged();

        return toRemove.Count;
    }

    /// <summary>
    /// Проверяет, назначен ли ресурс на задачу.
    /// </summary>
    public bool IsAssigned(Guid taskId, Guid resourceId)
    {
        return _assignments.Any(a => a.Matches(taskId, resourceId));
    }

    /// <summary>
    /// Получает все назначения ресурса.
    /// </summary>
    public IEnumerable<ResourceAssignment> GetAssignmentsByResource(Guid resourceId)
    {
        return _assignments.Where(a => a.ResourceId == resourceId);
    }

    #endregion

    #region ParticipationInterval Operations

    /// <summary>
    /// Добавляет интервал участия.
    /// </summary>
    /// <param name="interval">Интервал для добавления.</param>
    /// <exception cref="InvalidOperationException">Если интервал пересекается с существующим.</exception>
    public void AddParticipationInterval(ParticipationInterval interval)
    {
        if (interval == null)
            throw new ArgumentNullException(nameof(interval));

        var errors = interval.Validate();
        if (errors.Count > 0)
            throw new InvalidOperationException($"Невалидный интервал: {string.Join(", ", errors)}");

        // Проверяем, что ресурс существует
        var resource = GetResource(interval.ResourceId);
        if (resource == null)
            throw new InvalidOperationException($"Ресурс с ID {interval.ResourceId} не найден.");

        // Для Constructor MaxWorkload всегда = 100
        if (resource.Role == ResourceRole.Constructor)
        {
            interval.MaxWorkload = 100;
        }

        // Проверяем пересечение с существующими интервалами
        var existingIntervals = _participationIntervals
            .Where(i => i.ResourceId == interval.ResourceId);

        foreach (var existing in existingIntervals)
        {
            if (interval.OverlapsWith(existing))
            {
                throw new InvalidOperationException(
                    $"Интервал пересекается с существующим: {existing}");
            }
        }

        _participationIntervals.Add(interval);
        OnParticipationIntervalsChanged();
    }

    /// <summary>
    /// Обновляет интервал участия.
    /// </summary>
    public void UpdateParticipationInterval(ParticipationInterval interval)
    {
        if (interval == null)
            throw new ArgumentNullException(nameof(interval));

        var index = _participationIntervals.FindIndex(i => i.Id == interval.Id);
        if (index < 0)
            throw new InvalidOperationException($"Интервал с ID {interval.Id} не найден.");

        var errors = interval.Validate();
        if (errors.Count > 0)
            throw new InvalidOperationException($"Невалидный интервал: {string.Join(", ", errors)}");

        // Проверяем, что ресурс существует
        var resource = GetResource(interval.ResourceId);
        if (resource == null)
            throw new InvalidOperationException($"Ресурс с ID {interval.ResourceId} не найден.");

        // Для Constructor MaxWorkload всегда = 100
        if (resource.Role == ResourceRole.Constructor)
        {
            interval.MaxWorkload = 100;
        }

        // Проверяем пересечение (исключая самого себя)
        var existingIntervals = _participationIntervals
            .Where(i => i.ResourceId == interval.ResourceId && i.Id != interval.Id);

        foreach (var existing in existingIntervals)
        {
            if (interval.OverlapsWith(existing))
            {
                throw new InvalidOperationException(
                    $"Интервал пересекается с существующим: {existing}");
            }
        }

        _participationIntervals[index] = interval;
        OnParticipationIntervalsChanged();
    }

    /// <summary>
    /// Удаляет интервал участия.
    /// </summary>
    public bool RemoveParticipationInterval(Guid intervalId)
    {
        var interval = _participationIntervals.FirstOrDefault(i => i.Id == intervalId);
        if (interval == null)
            return false;

        _participationIntervals.Remove(interval);
        OnParticipationIntervalsChanged();
        return true;
    }

    /// <summary>
    /// Получает все интервалы участия для ресурса.
    /// </summary>
    public IEnumerable<ParticipationInterval> GetParticipationIntervalsForResource(Guid resourceId)
    {
        return _participationIntervals
            .Where(i => i.ResourceId == resourceId)
            .OrderBy(i => i.Start);
    }

    /// <summary>
    /// Получает интервал участия, в который попадает указанный день.
    /// </summary>
    public ParticipationInterval? GetParticipationIntervalForDay(Guid resourceId, TimeSpan day)
    {
        return _participationIntervals
            .FirstOrDefault(i => i.ResourceId == resourceId && i.ContainsDay(day));
    }

    /// <summary>
    /// Проверяет, участвует ли ресурс в указанный день.
    /// </summary>
    public bool IsResourceParticipating(Guid resourceId, TimeSpan day)
    {
        return GetParticipationIntervalForDay(resourceId, day) != null;
    }

    #endregion

    #region Absence Operations

    /// <summary>
    /// Добавляет отсутствие.
    /// </summary>
    public void AddAbsence(Absence absence)
    {
        if (absence == null)
            throw new ArgumentNullException(nameof(absence));

        var errors = absence.Validate();
        if (errors.Count > 0)
            throw new InvalidOperationException($"Невалидное отсутствие: {string.Join(", ", errors)}");

        // Проверяем, что ресурс существует
        if (GetResource(absence.ResourceId) == null)
            throw new InvalidOperationException($"Ресурс с ID {absence.ResourceId} не найден.");

        _absences.Add(absence);
        OnAbsencesChanged();
    }

    /// <summary>
    /// Обновляет отсутствие.
    /// </summary>
    public void UpdateAbsence(Absence absence)
    {
        if (absence == null)
            throw new ArgumentNullException(nameof(absence));

        var index = _absences.FindIndex(a => a.Id == absence.Id);
        if (index < 0)
            throw new InvalidOperationException($"Отсутствие с ID {absence.Id} не найдено.");

        var errors = absence.Validate();
        if (errors.Count > 0)
            throw new InvalidOperationException($"Невалидное отсутствие: {string.Join(", ", errors)}");

        _absences[index] = absence;
        OnAbsencesChanged();
    }

    /// <summary>
    /// Удаляет отсутствие.
    /// </summary>
    public bool RemoveAbsence(Guid absenceId)
    {
        var absence = _absences.FirstOrDefault(a => a.Id == absenceId);
        if (absence == null)
            return false;

        _absences.Remove(absence);
        OnAbsencesChanged();
        return true;
    }

    /// <summary>
    /// Получает все отсутствия для ресурса.
    /// </summary>
    public IEnumerable<Absence> GetAbsencesForResource(Guid resourceId)
    {
        return _absences
            .Where(a => a.ResourceId == resourceId)
            .OrderBy(a => a.Start);
    }

    /// <summary>
    /// Получает отсутствие, в которое попадает указанный день.
    /// </summary>
    public Absence? GetAbsenceForDay(Guid resourceId, TimeSpan day)
    {
        return _absences
            .FirstOrDefault(a => a.ResourceId == resourceId && a.ContainsDay(day));
    }

    /// <summary>
    /// Проверяет, отсутствует ли ресурс в указанный день.
    /// </summary>
    public bool IsResourceAbsent(Guid resourceId, TimeSpan day)
    {
        return GetAbsenceForDay(resourceId, day) != null;
    }

    #endregion

    #region Query Operations

    /// <summary>
    /// Получает все ресурсы, назначенные на задачу.
    /// </summary>
    public IEnumerable<Resource> GetResourcesForTask(Guid taskId)
    {
        var resourceIds = _assignments
            .Where(a => a.TaskId == taskId)
            .Select(a => a.ResourceId)
            .ToList();

        return _resources.Where(r => resourceIds.Contains(r.Id));
    }

    /// <summary>
    /// Получает все ресурсы, назначенные на задачу (перегрузка).
    /// </summary>
    public IEnumerable<Resource> GetResourcesForTask(Task task)
    {
        if (task == null) return Enumerable.Empty<Resource>();
        return GetResourcesForTask(task.Id);
    }

    /// <summary>
    /// Получает все задачи, на которые назначен ресурс.
    /// </summary>
    public IEnumerable<Guid> GetTasksForResource(Guid resourceId)
    {
        return _assignments
            .Where(a => a.ResourceId == resourceId)
            .Select(a => a.TaskId);
    }

    /// <summary>
    /// Получает строку с инициалами всех ресурсов задачи.
    /// Используется для отображения над баром на диаграмме.
    /// </summary>
    public string GetInitialsForTask(Guid taskId, string separator = ", ")
    {
        var resources = GetResourcesForTask(taskId).ToList();
        if (resources.Count == 0)
            return string.Empty;

        return string.Join(separator, resources.Select(r => r.Initials));
    }

    /// <summary>
    /// Получает строку с инициалами всех ресурсов задачи (перегрузка).
    /// </summary>
    public string GetInitialsForTask(Task task, string separator = ", ")
    {
        if (task == null) return string.Empty;
        return GetInitialsForTask(task.Id, separator);
    }

    #endregion

    #region Bulk Operations (для сериализации)

    /// <summary>
    /// Загружает ресурсы из коллекции (заменяет существующие).
    /// </summary>
    public void LoadResources(IEnumerable<Resource> resources)
    {
        _resources.Clear();
        if (resources != null)
        {
            _resources.AddRange(resources);
        }

        OnResourcesChanged();
    }

    /// <summary>
    /// Загружает назначения из коллекции (заменяет существующие).
    /// </summary>
    public void LoadAssignments(IEnumerable<ResourceAssignment> assignments)
    {
        _assignments.Clear();
        if (assignments != null)
        {
            _assignments.AddRange(assignments);
        }

        OnAssignmentsChanged();
    }

    /// <summary>
    /// Загружает интервалы участия из коллекции (заменяет существующие).
    /// </summary>
    public void LoadParticipationIntervals(IEnumerable<ParticipationInterval> intervals)
    {
        _participationIntervals.Clear();
        if (intervals != null)
        {
            _participationIntervals.AddRange(intervals);
        }

        OnParticipationIntervalsChanged();
    }

    /// <summary>
    /// Загружает отсутствия из коллекции (заменяет существующие).
    /// </summary>
    public void LoadAbsences(IEnumerable<Absence> absences)
    {
        _absences.Clear();
        if (absences != null)
        {
            _absences.AddRange(absences);
        }

        OnAbsencesChanged();
    }

    /// <summary>
    /// Возвращает все данные для сериализации.
    /// </summary>
    public (List<Resource> Resources, List<ResourceAssignment> Assignments) GetAllData()
    {
        return (
            new List<Resource>(_resources),
            new List<ResourceAssignment>(_assignments)
        );
    }

    /// <summary>
    /// Возвращает все данные для сериализации (расширенная версия).
    /// </summary>
    public (
        List<Resource> Resources,
        List<ResourceAssignment> Assignments,
        List<ParticipationInterval> ParticipationIntervals,
        List<Absence> Absences
    ) GetAllDataExtended()
    {
        return (
            new List<Resource>(_resources),
            new List<ResourceAssignment>(_assignments),
            new List<ParticipationInterval>(_participationIntervals),
            new List<Absence>(_absences)
        );
    }

    /// <summary>
    /// Создаёт дефолтные интервалы участия для ресурсов, у которых их нет.
    /// Используется при миграции старых файлов.
    /// </summary>
    public void EnsureDefaultParticipationIntervals()
    {
        foreach (var resource in _resources)
        {
            var hasInterval = _participationIntervals.Any(i => i.ResourceId == resource.Id);
            if (!hasInterval)
            {
                var defaultInterval = ParticipationInterval.CreateDefault(resource.Id);
                
                // Для Constructor MaxWorkload всегда 100
                if (resource.Role == ResourceRole.Constructor)
                {
                    defaultInterval.MaxWorkload = 100;
                }

                _participationIntervals.Add(defaultInterval);
            }
        }

        OnParticipationIntervalsChanged();
    }

    #endregion

    #region Event Handlers

    protected virtual void OnResourcesChanged()
    {
        ResourcesChanged?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void OnAssignmentsChanged()
    {
        AssignmentsChanged?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void OnParticipationIntervalsChanged()
    {
        ParticipationIntervalsChanged?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void OnAbsencesChanged()
    {
        AbsencesChanged?.Invoke(this, EventArgs.Empty);
    }

    #endregion
}