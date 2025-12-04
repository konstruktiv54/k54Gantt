namespace Core.Interfaces;

/// <summary>
/// Интерфейс для команд, поддерживающих отмену и повтор.
/// Реализует паттерн Command для Undo/Redo функциональности.
/// </summary>
public interface IUndoableAction
{
    /// <summary>
    /// Описание действия для отображения в UI.
    /// Например: "Изменение начала 'Задача 1'" или "Группировка задач".
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Выполняет действие.
    /// </summary>
    void Execute();

    /// <summary>
    /// Отменяет действие, возвращая состояние к предыдущему.
    /// </summary>
    void Undo();
}
