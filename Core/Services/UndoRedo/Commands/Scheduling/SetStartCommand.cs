using Core.Interfaces;
using Task = Core.Interfaces.Task;

namespace Core.Services.UndoRedo.Commands.Scheduling;

/// <summary>
/// Команда изменения даты начала задачи.
/// Поддерживает отмену и повтор операции.
/// </summary>
public class SetStartCommand : IUndoableAction
{
    private readonly ProjectManager _projectManager;
    private readonly Task _task;
    private readonly TimeSpan _oldStart;
    private readonly TimeSpan _newStart;

    /// <summary>
    /// Создаёт команду изменения даты начала.
    /// </summary>
    /// <param name="projectManager">Менеджер проекта.</param>
    /// <param name="task">Задача для изменения.</param>
    /// <param name="oldStart">Старое значение начала.</param>
    /// <param name="newStart">Новое значение начала.</param>
    public SetStartCommand(
        ProjectManager projectManager,
        Task task,
        TimeSpan oldStart,
        TimeSpan newStart)
    {
        _projectManager = projectManager ?? throw new ArgumentNullException(nameof(projectManager));
        _task = task ?? throw new ArgumentNullException(nameof(task));
        _oldStart = oldStart;
        _newStart = newStart;
    }

    /// <summary>
    /// Описание команды для UI.
    /// </summary>
    public string Description => $"Изменение начала '{_task.Name}'";

    /// <summary>
    /// ID задачи (для возможного объединения команд).
    /// </summary>
    public Guid TaskId => _task.Id;

    /// <summary>
    /// Старое значение начала.
    /// </summary>
    public TimeSpan OldStart => _oldStart;

    /// <summary>
    /// Новое значение начала.
    /// </summary>
    public TimeSpan NewStart => _newStart;

    /// <summary>
    /// Выполняет изменение даты начала.
    /// </summary>
    public void Execute()
    {
        _projectManager.SetStart(_task, _newStart);
    }

    /// <summary>
    /// Отменяет изменение, возвращая старое значение.
    /// </summary>
    public void Undo()
    {
        _projectManager.SetStart(_task, _oldStart);
    }

    /// <summary>
    /// Создаёт объединённую команду с другой SetStartCommand для той же задачи.
    /// Используется для оптимизации при drag-операциях.
    /// </summary>
    /// <param name="other">Другая команда для объединения.</param>
    /// <returns>Новая команда с начальным значением из this и конечным из other.</returns>
    public SetStartCommand MergeWith(SetStartCommand other)
    {
        if (other.TaskId != TaskId)
            throw new InvalidOperationException("Невозможно объединить команды для разных задач.");

        return new SetStartCommand(_projectManager, _task, _oldStart, other._newStart);
    }
}
