namespace Wpf.Controls;

/// <summary>
/// Тип операции перетаскивания.
/// </summary>
public enum DragOperation
{
    None,
    Moving,         // Перемещение по времени (горизонтально)
    ResizingStart,  // Изменение начала (левый край)
    ResizingEnd,    // Изменение длительности (правый край)
    Reordering      // Переупорядочивание (вертикально)
}

/// <summary>
/// Зона попадания на баре задачи.
/// </summary>
public enum HitZone
{
    None,
    LeftEdge,   // Левый край (8px) — resize start
    Center,     // Центр — move
    RightEdge   // Правый край (8px) — resize end
}

/// <summary>
/// Состояние операции перетаскивания.
/// Используется для Command pattern (будущий Undo/Redo).
/// </summary>
public class DragState
{
    /// <summary>
    /// Тип операции.
    /// </summary>
    public DragOperation Operation { get; set; } = DragOperation.None;

    /// <summary>
    /// Задача, которую перетаскивают.
    /// </summary>
    public Core.Interfaces.Task? Task { get; set; }

    /// <summary>
    /// Начальная точка мыши.
    /// </summary>
    public System.Windows.Point StartPoint { get; set; }

    /// <summary>
    /// Оригинальное значение Start задачи.
    /// </summary>
    public TimeSpan OriginalStart { get; set; }

    /// <summary>
    /// Оригинальное значение Duration задачи.
    /// </summary>
    public TimeSpan OriginalDuration { get; set; }

    /// <summary>
    /// Оригинальный индекс задачи (для Reordering).
    /// </summary>
    public int OriginalIndex { get; set; }

    /// <summary>
    /// Сбрасывает состояние.
    /// </summary>
    public void Reset()
    {
        Operation = DragOperation.None;
        Task = null;
        StartPoint = default;
        OriginalStart = TimeSpan.Zero;
        OriginalDuration = TimeSpan.Zero;
        OriginalIndex = -1;
    }

    /// <summary>
    /// Активна ли операция перетаскивания.
    /// </summary>
    public bool IsActive => Operation != DragOperation.None && Task != null;
}

/// <summary>
/// Аргументы события изменения задачи (для будущего Undo/Redo).
/// </summary>
public class TaskDragEventArgs : EventArgs
{
    public Core.Interfaces.Task Task { get; }
    public DragOperation Operation { get; }
    public TimeSpan OldStart { get; }
    public TimeSpan NewStart { get; }
    public TimeSpan OldDuration { get; }
    public TimeSpan NewDuration { get; }
    public int OldIndex { get; }
    public int NewIndex { get; }

    public TaskDragEventArgs(
        Core.Interfaces.Task task,
        DragOperation operation,
        TimeSpan oldStart, TimeSpan newStart,
        TimeSpan oldDuration, TimeSpan newDuration,
        int oldIndex = -1, int newIndex = -1)
    {
        Task = task;
        Operation = operation;
        OldStart = oldStart;
        NewStart = newStart;
        OldDuration = oldDuration;
        NewDuration = newDuration;
        OldIndex = oldIndex;
        NewIndex = newIndex;
    }
}