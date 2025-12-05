using Core.Interfaces;
using Task = Core.Interfaces.Task;

namespace Core.Services.UndoRedo.Commands.Scheduling;

/// <summary>
/// Команда изменения даты окончания задачи.
/// Поддерживает отмену и повтор операции.
/// </summary>
public class SetEndCommand : IUndoableAction
{
    private readonly ProjectManager _projectManager;
    private readonly Task _task;
    private readonly TimeSpan _oldEnd;
    private readonly TimeSpan _newEnd;

    /// <summary>
    /// Создаёт команду изменения даты окончания.
    /// </summary>
    /// <param name="projectManager">Менеджер проекта.</param>
    /// <param name="task">Задача для изменения.</param>
    /// <param name="oldEnd">Старое значение окончания.</param>
    /// <param name="newEnd">Новое значение окончания.</param>
    public SetEndCommand(
        ProjectManager projectManager,
        Task task,
        TimeSpan oldEnd,
        TimeSpan newEnd)
    {
        _projectManager = projectManager ?? throw new ArgumentNullException(nameof(projectManager));
        _task = task ?? throw new ArgumentNullException(nameof(task));
        _oldEnd = oldEnd;
        _newEnd = newEnd;
    }

    /// <summary>
    /// Описание команды для UI.
    /// </summary>
    public string Description => $"Изменение длительности '{_task.Name}'";

    /// <summary>
    /// ID задачи (для возможного объединения команд).
    /// </summary>
    public Guid TaskId => _task.Id;

    /// <summary>
    /// Старое значение окончания.
    /// </summary>
    public TimeSpan OldEnd => _oldEnd;

    /// <summary>
    /// Новое значение окончания.
    /// </summary>
    public TimeSpan NewEnd => _newEnd;

    /// <summary>
    /// Выполняет изменение даты окончания.
    /// </summary>
    public void Execute()
    {
        _projectManager.SetEnd(_task, _newEnd);
    }

    /// <summary>
    /// Отменяет изменение, возвращая старое значение.
    /// </summary>
    public void Undo()
    {
        _projectManager.SetEnd(_task, _oldEnd);
    }

    /// <summary>
    /// Создаёт объединённую команду с другой SetEndCommand для той же задачи.
    /// Используется для оптимизации при drag-операциях.
    /// </summary>
    /// <param name="other">Другая команда для объединения.</param>
    /// <returns>Новая команда с начальным значением из this и конечным из other.</returns>
    public SetEndCommand MergeWith(SetEndCommand other)
    {
        if (other.TaskId != TaskId)
            throw new InvalidOperationException("Невозможно объединить команды для разных задач.");

        return new SetEndCommand(_projectManager, _task, _oldEnd, other._newEnd);
    }
}
