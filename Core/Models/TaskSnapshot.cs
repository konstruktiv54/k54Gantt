namespace Core.Models;

/// <summary>
/// Снимок задачи для буфера обмена.
/// Хранит все данные задачи без привязки к ProjectManager.
/// Поддерживает иерархию (группы) и split-части.
/// </summary>
[Serializable]
public class TaskSnapshot
{
    #region Basic Properties

    /// <summary>
    /// Имя задачи (уже с суффиксом копии, например "Задача1").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Время начала относительно старта проекта.
    /// </summary>
    public TimeSpan Start { get; set; }

    /// <summary>
    /// Время окончания относительно старта проекта.
    /// </summary>
    public TimeSpan End { get; set; }

    /// <summary>
    /// Длительность задачи.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Процент выполнения (0.0 - 1.0).
    /// </summary>
    public float Complete { get; set; }

    /// <summary>
    /// Крайний срок (опционально).
    /// </summary>
    public TimeSpan? Deadline { get; set; }

    /// <summary>
    /// Заметка к задаче.
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// Состояние свёрнутости (для групп).
    /// </summary>
    public bool IsCollapsed { get; set; }

    #endregion

    #region Hierarchy Properties

    /// <summary>
    /// Дочерние задачи (для групп).
    /// Пустой список если задача не является группой.
    /// </summary>
    public List<TaskSnapshot> Children { get; set; } = new();

    /// <summary>
    /// Части split-задачи.
    /// Пустой список если задача не является split.
    /// </summary>
    public List<TaskSnapshot> SplitParts { get; set; } = new();

    /// <summary>
    /// Является ли задача split-задачей.
    /// </summary>
    public bool IsSplit { get; set; }

    /// <summary>
    /// Является ли задача группой.
    /// </summary>
    public bool IsGroup => Children.Count > 0;

    #endregion

    #region Factory Methods

    /// <summary>
    /// Создаёт пустой снимок.
    /// </summary>
    public TaskSnapshot()
    {
    }

    /// <summary>
    /// Создаёт снимок с базовыми данными.
    /// </summary>
    public TaskSnapshot(string name, TimeSpan start, TimeSpan end, TimeSpan duration)
    {
        Name = name;
        Start = start;
        End = end;
        Duration = duration;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Подсчитывает общее количество задач в снимке (включая детей и split-части).
    /// </summary>
    public int CountTotal()
    {
        var count = 1; // сама задача

        foreach (var child in Children)
        {
            count += child.CountTotal();
        }

        // Split-части не считаем отдельно, т.к. они часть одной задачи
        return count;
    }

    /// <summary>
    /// Возвращает строковое представление снимка.
    /// </summary>
    public override string ToString()
    {
        var type = IsSplit ? "[Split]" : IsGroup ? "[Group]" : "[Task]";
        return $"{type} {Name} ({Start:d\\.hh} - {End:d\\.hh})";
    }

    #endregion
}