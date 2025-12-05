using Core.Interfaces;

namespace Core.Services.UndoRedo;

/// <summary>
/// Составная команда, объединяющая несколько команд в одну транзакцию.
/// При Undo выполняет отмену всех команд в обратном порядке.
/// Используется для drag-операций и других составных действий.
/// </summary>
public class CompositeCommand : IUndoableAction
{
    private readonly List<IUndoableAction> _commands = new();
    private readonly string _description;

    /// <summary>
    /// Создаёт составную команду с указанным описанием.
    /// </summary>
    /// <param name="description">Описание для отображения в UI.</param>
    public CompositeCommand(string description)
    {
        _description = description;
    }

    /// <summary>
    /// Описание составной команды.
    /// </summary>
    public string Description => _description;

    /// <summary>
    /// Количество команд в составной команде.
    /// </summary>
    public int Count => _commands.Count;

    /// <summary>
    /// Проверяет, пуста ли составная команда.
    /// </summary>
    public bool IsEmpty => _commands.Count == 0;

    /// <summary>
    /// Добавляет команду в составную команду.
    /// Команда добавляется в конец списка.
    /// </summary>
    /// <param name="command">Команда для добавления.</param>
    public void Add(IUndoableAction command)
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));

        _commands.Add(command);
    }

    /// <summary>
    /// Выполняет все команды в порядке добавления.
    /// </summary>
    public void Execute()
    {
        foreach (var command in _commands)
        {
            command.Execute();
        }
    }

    /// <summary>
    /// Отменяет все команды в обратном порядке.
    /// Это критически важно для корректной отмены составных операций.
    /// </summary>
    public void Undo()
    {
        // Отменяем в обратном порядке!
        for (var i = _commands.Count - 1; i >= 0; i--)
        {
            _commands[i].Undo();
        }
    }

    /// <summary>
    /// Очищает все команды из составной команды.
    /// </summary>
    public void Clear()
    {
        _commands.Clear();
    }
}
