using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core.Services;
using Task = Core.Interfaces.Task;

namespace Wpf.ViewModels;

/// <summary>
/// ViewModel для отображения задачи в TreeView и DataGrid.
/// Поддерживает иерархию групп и split-частей.
/// </summary>
public partial class TaskItemViewModel : ObservableObject
{
    #region Private Fields

    private readonly ProjectManager _manager;
    private readonly DateTime _projectStart;
    private readonly Action _onExpandChanged;
    private readonly Action<TaskItemViewModel>? _onShowPartsChanged;

    #endregion

    #region Constructor

    /// <summary>
    /// Создаёт ViewModel для задачи.
    /// </summary>
    /// <param name="task">Оригинальная задача из Core.</param>
    /// <param name="manager">ProjectManager для операций изменения.</param>
    /// <param name="projectStart">Дата начала проекта.</param>
    /// <param name="level">Уровень вложенности (0 = корень).</param>
    /// <param name="parent">Родительский элемент (null для корневых).</param>
    /// <param name="onExpandChanged">Callback при изменении IsExpanded.</param>
    /// <param name="onShowPartsChanged">Callback при изменении ShowParts.</param>
    public TaskItemViewModel(
        Task task,
        ProjectManager manager,
        DateTime projectStart,
        int level = 0,
        TaskItemViewModel? parent = null,
        Action? onExpandChanged = null,
        Action<TaskItemViewModel>? onShowPartsChanged = null)
    {
        Task = task ?? throw new ArgumentNullException(nameof(task));
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _projectStart = projectStart;
        Level = level;
        Parent = parent;
        _onExpandChanged = onExpandChanged ?? (() => { });
        _onShowPartsChanged = onShowPartsChanged;

        // Инициализация состояния
        _isExpanded = !task.IsCollapsed;
        _showParts = false;

        // Определяем тип задачи
        IsGroup = manager.IsGroup(task);
        IsSplitRoot = manager.IsSplit(task);
        IsPart = manager.IsPart(task);

        // Инициализируем коллекцию детей
        Children = new ObservableCollection<TaskItemViewModel>();
    }

    #endregion

    #region Core Task Reference

    /// <summary>
    /// Ссылка на оригинальную задачу из Core.
    /// </summary>
    public Task Task { get; }

    /// <summary>
    /// Уникальный идентификатор задачи.
    /// </summary>
    public Guid Id => Task.Id;

    #endregion

    #region Editable Properties

    /// <summary>
    /// Имя задачи (редактируемое).
    /// </summary>
    public string Name
    {
        get => Task.Name;
        set
        {
            if (Task.Name != value)
            {
                Task.Name = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Дата начала задачи (редактируемая).
    /// Вычисляется как ProjectStart + Task.Start.
    /// </summary>
    public DateTime StartDate
    {
        get => _projectStart.Add(Task.Start);
        set
        {
            var newStart = value - _projectStart;
            if (newStart < TimeSpan.Zero)
                newStart = TimeSpan.Zero;

            if (Task.Start != newStart)
            {
                _manager.SetStart(Task, newStart);
                OnPropertyChanged();
                OnPropertyChanged(nameof(EndDate));
            }
        }
    }

    /// <summary>
    /// Дата окончания задачи (только чтение).
    /// </summary>
    public DateTime EndDate => _projectStart.Add(Task.End);

    /// <summary>
    /// Длительность в днях (редактируемая).
    /// </summary>
    public int DurationDays
    {
        get => (int)Math.Round(Task.Duration.TotalDays);
        set
        {
            if (value < 1) value = 1;
            var newDuration = TimeSpan.FromDays(value);

            if (Task.Duration != newDuration)
            {
                _manager.SetDuration(Task, newDuration);
                OnPropertyChanged();
                OnPropertyChanged(nameof(EndDate));
            }
        }
    }

    /// <summary>
    /// Процент выполнения (0-100, редактируемый).
    /// </summary>
    public int CompletePercent
    {
        get => (int)Math.Round(Task.Complete * 100);
        set
        {
            if (value < 0) value = 0;
            if (value > 100) value = 100;

            var newComplete = value / 100f;
            if (Math.Abs(Task.Complete - newComplete) > 0.001f)
            {
                _manager.SetComplete(Task, newComplete);
                OnPropertyChanged();
            }
        }
    }

    #endregion

    #region Hierarchy Properties

    /// <summary>
    /// Уровень вложенности (0 = корень).
    /// </summary>
    public int Level { get; }

    /// <summary>
    /// Родительский элемент (null для корневых задач).
    /// </summary>
    public TaskItemViewModel? Parent { get; }

    /// <summary>
    /// Дочерние элементы (подзадачи или split-части).
    /// </summary>
    public ObservableCollection<TaskItemViewModel> Children { get; }

    /// <summary>
    /// Является ли задача группой.
    /// </summary>
    public bool IsGroup { get; }

    /// <summary>
    /// Является ли задача split-задачей (имеет части).
    /// </summary>
    public bool IsSplitRoot { get; }

    /// <summary>
    /// Является ли задача частью split-задачи.
    /// </summary>
    public bool IsPart { get; }

    /// <summary>
    /// Есть ли дочерние элементы.
    /// </summary>
    public bool HasChildren => Children.Count > 0;

    #endregion

    #region UI State Properties

    [ObservableProperty]
    private bool _isExpanded;

    partial void OnIsExpandedChanged(bool value)
    {
        // Синхронизируем с моделью
        if (IsGroup)
        {
            _manager.SetCollapse(Task, !value);
        }
        
        // Уведомляем об изменении для обновления FlatTasks
        _onExpandChanged?.Invoke();
    }

    [ObservableProperty]
    private bool _showParts;

    partial void OnShowPartsChanged(bool value)
    {
        // Уведомляем об изменении для перестройки Children
        _onShowPartsChanged?.Invoke(this);
    }

    [ObservableProperty]
    private bool _isSelected;

    #endregion

    #region Display Properties

    /// <summary>
    /// Иконка для TreeView (Material Design Icon name).
    /// </summary>
    public string IconKind
    {
        get
        {
            if (IsGroup)
                return IsExpanded ? "FolderOpen" : "Folder";
            if (IsSplitRoot)
                return ShowParts ? "CallSplit" : "FileDocumentMultiple";
            if (IsPart)
                return "FileDocumentOutline";
            return "FileDocument";
        }
    }

    /// <summary>
    /// Отступ для отображения иерархии.
    /// </summary>
    public double Indent => Level * 16;

    /// <summary>
    /// Tooltip с полной информацией о задаче.
    /// </summary>
    public string ToolTip =>
        $"{Name}\n" +
        $"Начало: {StartDate:dd.MM.yyyy}\n" +
        $"Окончание: {EndDate:dd.MM.yyyy}\n" +
        $"Длительность: {DurationDays} дн.\n" +
        $"Выполнено: {CompletePercent}%";

    #endregion

    #region Commands

    /// <summary>
    /// Команда переключения отображения split-частей.
    /// </summary>
    [RelayCommand]
    private void ToggleShowParts()
    {
        if (IsSplitRoot)
        {
            ShowParts = !ShowParts;
        }
    }

    /// <summary>
    /// Команда переключения развёрнутости группы.
    /// </summary>
    [RelayCommand]
    private void ToggleExpand()
    {
        if (IsGroup || (IsSplitRoot && ShowParts))
        {
            IsExpanded = !IsExpanded;
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Обновляет все свойства из модели Task.
    /// Вызывается при изменении задачи извне.
    /// </summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(StartDate));
        OnPropertyChanged(nameof(EndDate));
        OnPropertyChanged(nameof(DurationDays));
        OnPropertyChanged(nameof(CompletePercent));
        OnPropertyChanged(nameof(IconKind));
        OnPropertyChanged(nameof(ToolTip));
    }

    /// <summary>
    /// Возвращает строковое представление.
    /// </summary>
    public override string ToString()
    {
        var prefix = IsGroup ? "[G]" : IsSplitRoot ? "[S]" : IsPart ? "[P]" : "";
        return $"{prefix} {Name} (Level {Level})";
    }

    #endregion
}