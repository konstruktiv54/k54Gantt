using Core.Interfaces;
using Task = Core.Interfaces.Task;

namespace Core.Services.UndoRedo.Commands.Scheduling;

/// <summary>
/// Команда изменения процента выполнения задачи.
/// Поддерживает отмену и повтор операции.
/// </summary>
public class SetCompleteCommand : IUndoableAction
{
    private readonly ProjectManager _projectManager;
    private readonly Task _task;
    private readonly float _oldComplete;
    private readonly float _newComplete;

    /// <summary>
    /// Создаёт команду изменения процента выполнения.
    /// </summary>
    /// <param name="projectManager">Менеджер проекта.</param>
    /// <param name="task">Задача для изменения.</param>
    /// <param name="oldComplete">Старое значение (0.0 - 1.0).</param>
    /// <param name="newComplete">Новое значение (0.0 - 1.0).</param>
    public SetCompleteCommand(
        ProjectManager projectManager,
        Task task,
        float oldComplete,
        float newComplete)
    {
        _projectManager = projectManager ?? throw new ArgumentNullException(nameof(projectManager));
        _task = task ?? throw new ArgumentNullException(nameof(task));
        _oldComplete = oldComplete;
        _newComplete = newComplete;
    }

    /// <summary>
    /// Описание команды для UI.
    /// </summary>
    public string Description => $"Изменение прогресса '{_task.Name}' ({(int)(_newComplete * 100)}%)";

    /// <summary>
    /// ID задачи.
    /// </summary>
    public Guid TaskId => _task.Id;

    /// <summary>
    /// Старое значение прогресса.
    /// </summary>
    public float OldComplete => _oldComplete;

    /// <summary>
    /// Новое значение прогресса.
    /// </summary>
    public float NewComplete => _newComplete;

    /// <summary>
    /// Выполняет изменение прогресса.
    /// </summary>
    public void Execute()
    {
        _projectManager.SetComplete(_task, _newComplete);
    }

    /// <summary>
    /// Отменяет изменение, возвращая старое значение.
    /// </summary>
    public void Undo()
    {
        _projectManager.SetComplete(_task, _oldComplete);
    }

    /// <summary>
    /// Создаёт объединённую команду для той же задачи.
    /// </summary>
    public SetCompleteCommand MergeWith(SetCompleteCommand other)
    {
        if (other.TaskId != TaskId)
            throw new InvalidOperationException("Невозможно объединить команды для разных задач.");

        return new SetCompleteCommand(_projectManager, _task, _oldComplete, other._newComplete);
    }
}
