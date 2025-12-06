using Core.Interfaces;

namespace Core.Services.UndoRedo;

/// <summary>
/// Сервис управления историей отмены и повтора действий.
/// Поддерживает транзакции для группировки нескольких операций в одно действие.
/// Ограничивает глубину истории для экономии памяти.
/// </summary>
public class UndoRedoService
{
    #region Constants

    /// <summary>
    /// Максимальная глубина истории отмены.
    /// </summary>
    private const int MaxUndoDepth = 5;

    #endregion

    #region Fields

    private readonly LinkedList<IUndoableAction> _undoStack = new();
    private readonly Stack<IUndoableAction> _redoStack = new();
    private CompositeCommand? _currentTransaction;
    private bool _isUndoingOrRedoing;

    #endregion

    #region Events

    /// <summary>
    /// Событие, возникающее при изменении состояния стеков Undo/Redo.
    /// Используется для обновления UI (доступность кнопок, описания).
    /// </summary>
    public event EventHandler? StateChanged;

    #endregion

    #region Properties

    /// <summary>
    /// Можно ли выполнить отмену.
    /// </summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>
    /// Можно ли выполнить повтор.
    /// </summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// Описание следующего действия для отмены.
    /// </summary>
    public string? UndoDescription => _undoStack.Last?.Value.Description;

    /// <summary>
    /// Описание следующего действия для повтора.
    /// </summary>
    public string? RedoDescription => _redoStack.Count > 0 ? _redoStack.Peek().Description : null;

    /// <summary>
    /// Количество действий в стеке отмены.
    /// </summary>
    public int UndoCount => _undoStack.Count;

    /// <summary>
    /// Количество действий в стеке повтора.
    /// </summary>
    public int RedoCount => _redoStack.Count;

    /// <summary>
    /// Находится ли сервис в процессе отмены или повтора.
    /// Используется для предотвращения рекурсивных вызовов.
    /// </summary>
    public bool IsUndoingOrRedoing => _isUndoingOrRedoing;

    /// <summary>
    /// Активна ли транзакция.
    /// </summary>
    public bool IsTransactionActive => _currentTransaction != null;

    #endregion

    #region Public Methods

    /// <summary>
    /// Выполняет команду и добавляет её в историю.
    /// Если активна транзакция, команда добавляется в неё.
    /// </summary>
    /// <param name="action">Команда для выполнения.</param>
    /// <param name="execute">Выполнять ли команду (false если уже выполнена).</param>
    public void Execute(IUndoableAction action, bool execute = true)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        // Не записываем в историю во время Undo/Redo
        if (_isUndoingOrRedoing)
            return;

        // Выполняем команду
        if (execute)
        {
            action.Execute();
        }

        // Добавляем в транзакцию или в стек
        if (_currentTransaction != null)
        {
            _currentTransaction.Add(action);
        }
        else
        {
            PushToUndoStack(action);
            _redoStack.Clear(); // Очищаем Redo при новом действии
            OnStateChanged();
        }
    }

    /// <summary>
    /// Отменяет последнее действие.
    /// </summary>
    public void Undo()
    {
        if (!CanUndo)
            return;

        _isUndoingOrRedoing = true;
        try
        {
            var action = _undoStack.Last!.Value;
            _undoStack.RemoveLast();

            action.Undo();

            _redoStack.Push(action);
            OnStateChanged();
        }
        finally
        {
            _isUndoingOrRedoing = false;
        }
    }

    /// <summary>
    /// Повторяет последнее отменённое действие.
    /// </summary>
    public void Redo()
    {
        if (!CanRedo)
            return;

        _isUndoingOrRedoing = true;
        try
        {
            var action = _redoStack.Pop();

            action.Execute();

            PushToUndoStack(action);
            OnStateChanged();
        }
        finally
        {
            _isUndoingOrRedoing = false;
        }
    }

    /// <summary>
    /// Начинает транзакцию для группировки нескольких команд.
    /// Все команды, выполненные до CommitTransaction, будут объединены в одну.
    /// </summary>
    /// <param name="description">Описание транзакции.</param>
    /// <exception cref="InvalidOperationException">Если транзакция уже активна.</exception>
    public void BeginTransaction(string description)
    {
        if (_currentTransaction != null)
            throw new InvalidOperationException("Транзакция уже активна. Вложенные транзакции не поддерживаются.");

        _currentTransaction = new CompositeCommand(description);
    }

    /// <summary>
    /// Фиксирует транзакцию, добавляя все её команды как одно действие.
    /// </summary>
    /// <exception cref="InvalidOperationException">Если транзакция не активна.</exception>
    public void CommitTransaction()
    {
        if (_currentTransaction == null)
            throw new InvalidOperationException("Нет активной транзакции для фиксации.");

        var transaction = _currentTransaction;
        _currentTransaction = null;

        // Добавляем только если были команды
        if (!transaction.IsEmpty)
        {
            PushToUndoStack(transaction);
            _redoStack.Clear();
            OnStateChanged();
        }
    }

    /// <summary>
    /// Откатывает транзакцию, отменяя все выполненные в ней команды.
    /// </summary>
    /// <exception cref="InvalidOperationException">Если транзакция не активна.</exception>
    public void RollbackTransaction()
    {
        if (_currentTransaction == null)
            throw new InvalidOperationException("Нет активной транзакции для отката.");

        var transaction = _currentTransaction;
        _currentTransaction = null;

        // Отменяем все команды транзакции
        if (!transaction.IsEmpty)
        {
            transaction.Undo();
        }
    }

    /// <summary>
    /// Очищает всю историю отмены и повтора.
    /// Вызывается при создании нового проекта или загрузке файла.
    /// </summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        _currentTransaction = null;
        OnStateChanged();
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Добавляет действие в стек отмены с учётом ограничения глубины.
    /// </summary>
    private void PushToUndoStack(IUndoableAction action)
    {
        _undoStack.AddLast(action);

        // Ограничиваем глубину истории
        while (_undoStack.Count > MaxUndoDepth)
        {
            _undoStack.RemoveFirst();
        }
    }

    /// <summary>
    /// Вызывает событие изменения состояния.
    /// </summary>
    protected virtual void OnStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    #endregion
}
