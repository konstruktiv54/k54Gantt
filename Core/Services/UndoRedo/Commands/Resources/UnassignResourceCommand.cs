using Core.Interfaces;
using Core.Models;

namespace Core.Services.UndoRedo.Commands.Resources;

/// <summary>
/// Команда снятия назначения ресурса с задачи.
/// Сохраняет workload для корректного восстановления при отмене.
/// </summary>
public class UnassignResourceCommand : IUndoableAction
{
    private readonly ResourceService _resourceService;
    private readonly Guid _taskId;
    private readonly Guid _resourceId;
    private readonly int _savedWorkload;
    private readonly string _taskName;
    private readonly string _resourceName;

    /// <summary>
    /// Создаёт команду снятия назначения ресурса.
    /// </summary>
    /// <param name="resourceService">Сервис ресурсов.</param>
    /// <param name="taskId">ID задачи.</param>
    /// <param name="resourceId">ID ресурса.</param>
    /// <param name="savedWorkload">Сохранённый workload для восстановления.</param>
    /// <param name="taskName">Название задачи (для описания).</param>
    /// <param name="resourceName">Название ресурса (для описания).</param>
    public UnassignResourceCommand(
        ResourceService resourceService,
        Guid taskId,
        Guid resourceId,
        int savedWorkload,
        string taskName,
        string resourceName)
    {
        _resourceService = resourceService ?? throw new ArgumentNullException(nameof(resourceService));
        _taskId = taskId;
        _resourceId = resourceId;
        _savedWorkload = savedWorkload;
        _taskName = taskName ?? "Задача";
        _resourceName = resourceName ?? "Ресурс";
    }

    /// <summary>
    /// Описание команды для UI.
    /// </summary>
    public string Description => $"Снятие '{_resourceName}' с '{_taskName}'";

    /// <summary>
    /// Выполняет снятие назначения ресурса.
    /// </summary>
    public void Execute()
    {
        _resourceService.UnassignResource(_taskId, _resourceId);
    }

    /// <summary>
    /// Отменяет снятие, восстанавливая назначение с сохранённым workload.
    /// </summary>
    public void Undo()
    {
        _resourceService.AssignResource(_taskId, _resourceId, _savedWorkload);
    }

    /// <summary>
    /// Создаёт команду из существующего назначения.
    /// Автоматически сохраняет текущий workload.
    /// </summary>
    public static UnassignResourceCommand FromAssignment(
        ResourceService resourceService,
        ResourceAssignment assignment,
        string taskName,
        string resourceName)
    {
        return new UnassignResourceCommand(
            resourceService,
            assignment.TaskId,
            assignment.ResourceId,
            assignment.Workload,
            taskName,
            resourceName);
    }
}
