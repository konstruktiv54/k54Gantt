using Task = Core.Interfaces.Task;

namespace Core.Services;

/// <summary>
/// Extension методы для ProjectManager.
/// </summary>
public static class ProjectManagerExtensions
{
    /// <summary>
    /// Получает задачу по её идентификатору.
    /// </summary>
    /// <param name="manager">Менеджер проекта.</param>
    /// <param name="taskId">Идентификатор задачи.</param>
    /// <returns>Задача или null, если не найдена.</returns>
    public static Task? GetTaskById(this ProjectManager manager, Guid taskId)
    {
        if (manager == null)
            return null;

        return manager.Tasks.FirstOrDefault(t => t.Id == taskId);
    }

    /// <summary>
    /// Получает задачу по её идентификатору (generic версия).
    /// </summary>
    /// <typeparam name="T">Тип задачи.</typeparam>
    /// <typeparam name="R">Тип ресурса.</typeparam>
    /// <param name="manager">Менеджер проекта.</param>
    /// <param name="taskId">Идентификатор задачи.</param>
    /// <returns>Задача или null, если не найдена.</returns>
    public static T? GetTaskById<T, R>(this ProjectManager<T, R> manager, Guid taskId)
        where T : Task
        where R : class
    {
        if (manager == null)
            return null;

        return manager.Tasks.FirstOrDefault(t => t.Id == taskId);
    }

    /// <summary>
    /// Проверяет, существует ли задача с указанным идентификатором.
    /// </summary>
    /// <param name="manager">Менеджер проекта.</param>
    /// <param name="taskId">Идентификатор задачи.</param>
    /// <returns>True, если задача существует.</returns>
    public static bool TaskExists(this ProjectManager manager, Guid taskId)
    {
        return manager?.Tasks.Any(t => t.Id == taskId) ?? false;
    }

    /// <summary>
    /// Получает минимальную дату начала среди всех задач.
    /// </summary>
    /// <param name="manager">Менеджер проекта.</param>
    /// <returns>Минимальный Start или TimeSpan.Zero.</returns>
    public static TimeSpan GetMinStart(this ProjectManager manager)
    {
        if (manager?.Tasks == null || manager.Tasks.Count == 0)
            return TimeSpan.Zero;

        return manager.Tasks.Min(t => t.Start);
    }

    /// <summary>
    /// Получает максимальную дату окончания среди всех задач.
    /// </summary>
    /// <param name="manager">Менеджер проекта.</param>
    /// <returns>Максимальный End или TimeSpan.Zero.</returns>
    public static TimeSpan GetMaxEnd(this ProjectManager manager)
    {
        if (manager?.Tasks == null || manager.Tasks.Count == 0)
            return TimeSpan.Zero;

        return manager.Tasks.Max(t => t.End);
    }

    /// <summary>
    /// Получает общую длительность проекта (от первой задачи до последней).
    /// </summary>
    /// <param name="manager">Менеджер проекта.</param>
    /// <returns>Длительность проекта в TimeSpan.</returns>
    public static TimeSpan GetProjectDuration(this ProjectManager manager)
    {
        var minStart = manager.GetMinStart();
        var maxEnd = manager.GetMaxEnd();
        return maxEnd - minStart;
    }
}