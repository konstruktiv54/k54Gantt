using Core.Models;
using Task = Core.Interfaces.Task;

namespace Core.Services;

/// <summary>
/// Сервис для управления ресурсами проекта и их назначениями на задачи.
/// Обеспечивает CRUD операции для ресурсов и управление связями ресурс-задача.
/// </summary>
public class ResourceService
{
    #region Fields

    private readonly List<Resource> _resources = new List<Resource>();
    private readonly List<ResourceAssignment> _assignments = new List<ResourceAssignment>();

    #endregion

    #region Events

    /// <summary>
    /// Событие, возникающее при изменении списка ресурсов.
    /// </summary>
    public event EventHandler ResourcesChanged;

    /// <summary>
    /// Событие, возникающее при изменении назначений.
    /// </summary>
    public event EventHandler AssignmentsChanged;

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
    /// </summary>
    public void AddResource(Resource resource)
    {
        if (resource == null)
            throw new ArgumentNullException(nameof(resource));

        if (_resources.Any(r => r.Id == resource.Id))
            throw new InvalidOperationException($"Ресурс с ID {resource.Id} уже существует.");

        _resources.Add(resource);
        OnResourcesChanged();
    }

    /// <summary>
    /// Получает назначение ресурса на задачу.
    /// </summary>
    public ResourceAssignment GetAssignment(Guid taskId, Guid resourceId)
    {
        return _assignments.FirstOrDefault(a => a.Matches(taskId, resourceId));
    }

    /// <summary>
    /// Создаёт и добавляет новый ресурс с указанным именем.
    /// </summary>
    public Resource CreateResource(string name, string role = "")
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

        _resources[index] = resource;
        OnResourcesChanged();
    }

    /// <summary>
    /// Удаляет ресурс по идентификатору.
    /// Также удаляет все назначения этого ресурса.
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

        _resources.Remove(resource);

        if (assignmentsToRemove.Count > 0)
            OnAssignmentsChanged();

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
    /// Получает ресурс по идентификатору.
    /// </summary>
    public Resource GetResource(Guid resourceId)
    {
        return _resources.FirstOrDefault(r => r.Id == resourceId);
    }

    /// <summary>
    /// Получает ресурс по имени (первое совпадение).
    /// </summary>
    public Resource GetResourceByName(string name)
    {
        return _resources.FirstOrDefault(r =>
            string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Очищает все ресурсы и назначения.
    /// </summary>
    public void Clear()
    {
        _resources.Clear();
        _assignments.Clear();
        OnResourcesChanged();
        OnAssignmentsChanged();
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
    /// Возвращает все данные для сериализации.
    /// </summary>
    public (List<Resource> Resources, List<ResourceAssignment> Assignments) GetAllData()
    {
        return (
            new List<Resource>(_resources),
            new List<ResourceAssignment>(_assignments)
        );
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

    #endregion
}