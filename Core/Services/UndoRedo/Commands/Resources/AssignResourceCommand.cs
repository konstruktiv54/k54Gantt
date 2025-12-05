using Core.Interfaces;

namespace Core.Services.UndoRedo.Commands.Resources;

/// <summary>
/// Команда назначения ресурса на задачу.
/// </summary>
public class AssignResourceCommand : IUndoableAction
{
    private readonly ResourceService _resourceService;
    private readonly Guid _taskId;
    private readonly Guid _resourceId;
    private readonly int _workload;
    private readonly string _taskName;
    private readonly string _resourceName;

    /// <summary>
    /// Создаёт команду назначения ресурса.
    /// </summary>
    /// <param name="resourceService">Сервис ресурсов.</param>
    /// <param name="taskId">ID задачи.</param>
    /// <param name="resourceId">ID ресурса.</param>
    /// <param name="workload">Загрузка в процентах (по умолчанию 100).</param>
    /// <param name="taskName">Название задачи (для описания).</param>
    /// <param name="resourceName">Название ресурса (для описания).</param>
    public AssignResourceCommand(
        ResourceService resourceService,
        Guid taskId,
        Guid resourceId,
        int workload,
        string taskName,
        string resourceName)
    {
        _resourceService = resourceService ?? throw new ArgumentNullException(nameof(resourceService));
        _taskId = taskId;
        _resourceId = resourceId;
        _workload = workload;
        _taskName = taskName ?? "Задача";
        _resourceName = resourceName ?? "Ресурс";
    }

    /// <summary>
    /// Описание команды для UI.
    /// </summary>
    public string Description => $"Назначение '{_resourceName}' на '{_taskName}'";

    /// <summary>
    /// Выполняет назначение ресурса.
    /// </summary>
    public void Execute()
    {
        _resourceService.AssignResource(_taskId, _resourceId, _workload);
    }

    /// <summary>
    /// Отменяет назначение ресурса.
    /// </summary>
    public void Undo()
    {
        _resourceService.UnassignResource(_taskId, _resourceId);
    }
}
