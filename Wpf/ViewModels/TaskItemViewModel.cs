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
    private readonly Action? _onTaskModified;

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
        Action<TaskItemViewModel>? onShowPartsChanged = null,
        Action? onTaskModified = null)
    {
        Task = task ?? throw new ArgumentNullException(nameof(task));
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _projectStart = projectStart;
        Level = level;
        Parent = parent;
        _onExpandChanged = onExpandChanged ?? (() => { });
        _onShowPartsChanged = onShowPartsChanged;
        _onTaskModified = onTaskModified;

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
                _onTaskModified?.Invoke();
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
            
            if (Task.Start == newStart) return;
            _manager.SetStart(Task, newStart);
            OnPropertyChanged();
            OnPropertyChanged(nameof(EndDate));
            _onTaskModified?.Invoke();
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

            if (Task.Duration == newDuration) return;
            _manager.SetDuration(Task, newDuration);
            OnPropertyChanged();
            OnPropertyChanged(nameof(EndDate));
            _onTaskModified?.Invoke();
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
                _onTaskModified?.Invoke();
            }
        }
    }
    
    /// <summary>
    /// Дата дедлайна (редактируемая).
    /// Null означает отсутствие дедлайна.
    /// </summary>
    public DateTime? DeadlineDate
    {
        get => Task.Deadline.HasValue 
            ? _projectStart.Add(Task.Deadline.Value) 
            : null;
        set
        {
            if (value.HasValue)
            {
                var newDeadline = value.Value - _projectStart;
                if (newDeadline < TimeSpan.Zero)
                    newDeadline = TimeSpan.Zero;
                
                // Deadline не может быть раньше End
                if (newDeadline < Task.End)
                    newDeadline = Task.End;

                if (Task.Deadline == newDeadline) return;
                _manager.SetDeadline(Task, newDeadline);
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasDeadline));
                OnPropertyChanged(nameof(DaysUntilDeadline));
                OnPropertyChanged(nameof(IsOverdue));
                _onTaskModified?.Invoke();
            }
            else
            {
                if (Task.Deadline.HasValue)
                {
                    _manager.SetDeadline(Task, null);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasDeadline));
                    OnPropertyChanged(nameof(DaysUntilDeadline));
                    OnPropertyChanged(nameof(IsOverdue));
                    _onTaskModified?.Invoke();
                }
            }
        }
    }
    
    /// <summary>
    /// Есть ли дедлайн у задачи.
    /// </summary>
    public bool HasDeadline => Task.Deadline.HasValue;

    /// <summary>
    /// Дней до дедлайна (отрицательное = просрочено).
    /// </summary>
    public int? DaysUntilDeadline
    {
        get
        {
            if (!Task.Deadline.HasValue) return null;
            return (int)(Task.Deadline.Value - Task.End).TotalDays;
        }
    }

    /// <summary>
    /// Просрочена ли задача (End > Deadline).
    /// </summary>
    public bool IsOverdue => Task.Deadline.HasValue && Task.End > Task.Deadline.Value;

    /// <summary>
    /// Заметка к задаче (редактируемая).
    /// </summary>
    public string? Note
    {
        get => Task.Note;
        set
        {
            if (Task.Note != value)
            {
                _manager.SetNote(Task, value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasNote));
                OnPropertyChanged(nameof(NotePreview));
                _onTaskModified?.Invoke();
            }
        }
    }

    /// <summary>
    /// Есть ли заметка у задачи.
    /// </summary>
    public bool HasNote => !string.IsNullOrWhiteSpace(Task.Note);

    /// <summary>
    /// Превью заметки (первые 50 символов).
    /// </summary>
    public string NotePreview
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Task.Note))
                return string.Empty;

            var preview = Task.Note
                .Replace("\r\n", " ")
                .Replace("\n", " ")
                .Replace("\r", " ")
                .Trim();

            return preview.Length > 50 
                ? preview.Substring(0, 47) + "..." 
                : preview;
        }
    }

    /// <summary>
    /// Развёрнута ли заметка в UI.
    /// </summary>
    [ObservableProperty]
    private bool _isNoteExpanded;

    partial void OnIsNoteExpandedChanged(bool value)
    {
        Task.IsNoteExpanded = value;
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
    public string ToolTip
    {
        get
        {
            var tip = $"{Name}\n" +
                      $"Начало: {StartDate:dd.MM.yyyy}\n" +
                      $"Окончание: {EndDate:dd.MM.yyyy}\n" +
                      $"Длительность: {DurationDays} дн.\n" +
                      $"Выполнено: {CompletePercent}%";

            if (HasDeadline)
            {
                tip += $"\nДедлайн: {DeadlineDate:dd.MM.yyyy}";
                
                if (DaysUntilDeadline.HasValue)
                {
                    var days = DaysUntilDeadline.Value;
                    if (days > 0)
                        tip += $" (осталось {days} дн.)";
                    else if (days < 0)
                        tip += $" (просрочено на {-days} дн.)";
                    else
                        tip += " (сегодня!)";
                }
            }

            if (HasNote)
            {
                tip += $"\n\nЗаметка: {NotePreview}";
            }

            return tip;
        }
    }

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
    
    /// <summary>
    /// Команда переключения развёрнутости заметки.
    /// </summary>
    [RelayCommand]
    private void ToggleNoteExpanded()
    {
        IsNoteExpanded = !IsNoteExpanded;
    }
    
    /// <summary>
    /// Команда очистки дедлайна.
    /// </summary>
    [RelayCommand]
    private void ClearDeadline()
    {
        DeadlineDate = null;
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
        
        OnPropertyChanged(nameof(DeadlineDate));
        OnPropertyChanged(nameof(HasDeadline));
        OnPropertyChanged(nameof(DaysUntilDeadline));
        OnPropertyChanged(nameof(IsOverdue));
        OnPropertyChanged(nameof(Note));
        OnPropertyChanged(nameof(HasNote));
        OnPropertyChanged(nameof(NotePreview));
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