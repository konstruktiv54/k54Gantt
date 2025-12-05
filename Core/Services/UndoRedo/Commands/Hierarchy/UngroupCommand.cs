using Core.Interfaces;
using Task = Core.Interfaces.Task;

namespace Core.Services.UndoRedo.Commands.Hierarchy;

/// <summary>
/// Команда удаления задачи из группы.
/// </summary>
public class UngroupCommand : IUndoableAction
{
    private readonly ProjectManager _projectManager;
    private readonly Task _group;
    private readonly Task _member;

    /// <summary>
    /// Создаёт команду разгруппировки.
    /// </summary>
    /// <param name="projectManager">Менеджер проекта.</param>
    /// <param name="group">Группа, из которой удаляется задача.</param>
    /// <param name="member">Задача для удаления из группы.</param>
    public UngroupCommand(
        ProjectManager projectManager,
        Task group,
        Task member)
    {
        _projectManager = projectManager ?? throw new ArgumentNullException(nameof(projectManager));
        _group = group ?? throw new ArgumentNullException(nameof(group));
        _member = member ?? throw new ArgumentNullException(nameof(member));
    }

    /// <summary>
    /// Описание команды для UI.
    /// </summary>
    public string Description => $"Удаление '{_member.Name}' из группы '{_group.Name}'";

    /// <summary>
    /// Выполняет удаление задачи из группы.
    /// </summary>
    public void Execute()
    {
        _projectManager.Ungroup(_group, _member);
    }

    /// <summary>
    /// Отменяет разгруппировку, возвращая задачу в группу.
    /// </summary>
    public void Undo()
    {
        _projectManager.Group(_group, _member);
    }
}
