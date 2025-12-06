using Core.Interfaces;
using Task = Core.Interfaces.Task;

namespace Core.Services.UndoRedo.Commands.Hierarchy;

/// <summary>
/// Команда переупорядочивания (перемещения) задачи в списке.
/// </summary>
public class MoveCommand : IUndoableAction
{
    private readonly ProjectManager _projectManager;
    private readonly Task _task;
    private readonly int _offset;

    /// <summary>
    /// Создаёт команду перемещения задачи.
    /// </summary>
    /// <param name="projectManager">Менеджер проекта.</param>
    /// <param name="task">Задача для перемещения.</param>
    /// <param name="offset">Смещение позиции (положительное — вниз, отрицательное — вверх).</param>
    public MoveCommand(
        ProjectManager projectManager,
        Task task,
        int offset)
    {
        _projectManager = projectManager ?? throw new ArgumentNullException(nameof(projectManager));
        _task = task ?? throw new ArgumentNullException(nameof(task));
        _offset = offset;
    }

    /// <summary>
    /// Описание команды для UI.
    /// </summary>
    public string Description => _offset > 0
        ? $"Перемещение '{_task.Name}' вниз"
        : $"Перемещение '{_task.Name}' вверх";

    /// <summary>
    /// Смещение позиции.
    /// </summary>
    public int Offset => _offset;

    /// <summary>
    /// Выполняет перемещение задачи.
    /// </summary>
    public void Execute()
    {
        _projectManager.Move(_task, _offset);
    }

    /// <summary>
    /// Отменяет перемещение, возвращая задачу на прежнюю позицию.
    /// </summary>
    public void Undo()
    {
        _projectManager.Move(_task, -_offset);
    }
}
