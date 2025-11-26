using Core.Services;
using Task = Core.Interfaces.Task;

namespace Core.Models;

/// <summary>
/// Расширенный класс задачи с дополнительной функциональностью.
/// </summary>
[Serializable]
public class MyTask : Task
{
    private readonly ProjectManager _manager;

    public MyTask()
    {
        _manager = null;
    }

    public MyTask(ProjectManager manager)
    {
        _manager = manager;
    }

    /// <summary>
    /// Устанавливает время начала задачи через ProjectManager.
    /// </summary>
    public void SetStart(TimeSpan start)
    {
        _manager?.SetStart(this, start);
    }

    /// <summary>
    /// Устанавливает продолжительность задачи через ProjectManager.
    /// </summary>
    public void SetDuration(TimeSpan duration)
    {
        _manager?.SetDuration(this, duration);
    }

    /// <summary>
    /// Устанавливает процент выполнения через ProjectManager.
    /// </summary>
    public void SetComplete(float complete)
    {
        _manager?.SetComplete(this, complete);
    }
}