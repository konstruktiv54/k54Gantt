namespace Core.Models;

/// <summary>
/// Буфер обмена для задач.
/// Хранит один или несколько снимков задач для операции вставки.
/// </summary>
[Serializable]
public class TaskClipboard
{
    #region Properties

    /// <summary>
    /// Снимки скопированных задач.
    /// Поддерживает множественное копирование.
    /// </summary>
    public List<TaskSnapshot> Snapshots { get; set; } = new();

    /// <summary>
    /// ID родительской группы для каждого снимка.
    /// Ключ — индекс в Snapshots, значение — ID группы-родителя (null если корневая).
    /// Используется для определения куда вставлять копию.
    /// </summary>
    public Dictionary<int, Guid?> SourceParentIds { get; set; } = new();

    /// <summary>
    /// ID оригинальных задач (для предотвращения вставки в саму себя).
    /// </summary>
    public List<Guid> SourceTaskIds { get; set; } = new();

    /// <summary>
    /// Время создания буфера.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    #endregion

    #region Computed Properties

    /// <summary>
    /// Есть ли данные в буфере.
    /// </summary>
    public bool HasData => Snapshots.Count > 0;

    /// <summary>
    /// Количество корневых задач в буфере.
    /// </summary>
    public int Count => Snapshots.Count;

    /// <summary>
    /// Общее количество задач (включая дочерние).
    /// </summary>
    public int TotalCount => Snapshots.Sum(s => s.CountTotal());

    /// <summary>
    /// Первый снимок (для совместимости с одиночным копированием).
    /// </summary>
    public TaskSnapshot? FirstSnapshot => Snapshots.FirstOrDefault();

    #endregion

    #region Factory Methods

    /// <summary>
    /// Создаёт пустой буфер.
    /// </summary>
    public TaskClipboard()
    {
    }

    /// <summary>
    /// Создаёт буфер с одним снимком.
    /// </summary>
    /// <param name="snapshot">Снимок задачи.</param>
    /// <param name="sourceParentId">ID родительской группы оригинала.</param>
    /// <param name="sourceTaskId">ID оригинальной задачи.</param>
    public static TaskClipboard Single(TaskSnapshot snapshot, Guid? sourceParentId, Guid sourceTaskId)
    {
        var clipboard = new TaskClipboard();
        clipboard.Snapshots.Add(snapshot);
        clipboard.SourceParentIds[0] = sourceParentId;
        clipboard.SourceTaskIds.Add(sourceTaskId);
        return clipboard;
    }

    /// <summary>
    /// Создаёт буфер с несколькими снимками.
    /// </summary>
    /// <param name="items">Список кортежей (снимок, ID родителя, ID оригинала).</param>
    public static TaskClipboard Multiple(IEnumerable<(TaskSnapshot Snapshot, Guid? ParentId, Guid SourceId)> items)
    {
        var clipboard = new TaskClipboard();
        var index = 0;

        foreach (var (snapshot, parentId, sourceId) in items)
        {
            clipboard.Snapshots.Add(snapshot);
            clipboard.SourceParentIds[index] = parentId;
            clipboard.SourceTaskIds.Add(sourceId);
            index++;
        }

        return clipboard;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Очищает буфер.
    /// </summary>
    public void Clear()
    {
        Snapshots.Clear();
        SourceParentIds.Clear();
        SourceTaskIds.Clear();
    }

    /// <summary>
    /// Получает ID родителя для снимка по индексу.
    /// </summary>
    public Guid? GetSourceParentId(int index)
    {
        return SourceParentIds.TryGetValue(index, out var parentId) ? parentId : null;
    }

    /// <summary>
    /// Проверяет, является ли задача источником копирования.
    /// </summary>
    public bool IsSourceTask(Guid taskId)
    {
        return SourceTaskIds.Contains(taskId);
    }

    /// <summary>
    /// Возвращает описание содержимого буфера.
    /// </summary>
    public override string ToString()
    {
        if (!HasData)
            return "Clipboard: Empty";

        if (Count == 1)
            return $"Clipboard: {FirstSnapshot?.Name} ({TotalCount} task(s))";

        return $"Clipboard: {Count} items ({TotalCount} total tasks)";
    }

    #endregion
}