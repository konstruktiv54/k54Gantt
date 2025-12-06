using Core.Interfaces;
using Task = Core.Interfaces.Task;

namespace Core.Services.UndoRedo.Commands.Hierarchy;

/// <summary>
/// Команда добавления задачи в группу.
/// Запоминает предыдущего родителя для корректной отмены.
/// </summary>
public class GroupCommand : IUndoableAction
{
    private readonly ProjectManager _projectManager;
    private readonly Task _group;
    private readonly Task _member;
    private readonly Task? _previousParent;

    /// <summary>
    /// Создаёт команду группировки.
    /// </summary>
    /// <param name="projectManager">Менеджер проекта.</param>
    /// <param name="group">Группа, в которую добавляется задача.</param>
    /// <param name="member">Задача для добавления в группу.</param>
    /// <param name="previousParent">Предыдущий родитель задачи (может быть null).</param>
    public GroupCommand(
        ProjectManager projectManager,
        Task group,
        Task member,
        Task? previousParent)
    {
        _projectManager = projectManager ?? throw new ArgumentNullException(nameof(projectManager));
        _group = group ?? throw new ArgumentNullException(nameof(group));
        _member = member ?? throw new ArgumentNullException(nameof(member));
        _previousParent = previousParent;
    }

    /// <summary>
    /// Описание команды для UI.
    /// </summary>
    public string Description => $"Добавление '{_member.Name}' в группу '{_group.Name}'";

    /// <summary>
    /// Выполняет добавление задачи в группу.
    /// </summary>
    public void Execute()
    {
        _projectManager.Group(_group, _member);
    }

    /// <summary>
    /// Отменяет группировку и восстанавливает предыдущего родителя.
    /// </summary>
    public void Undo()
    {
        _projectManager.Ungroup(_group, _member);

        // Восстанавливаем предыдущего родителя, если был
        if (_previousParent != null)
        {
            _projectManager.Group(_previousParent, _member);
        }
    }
}
