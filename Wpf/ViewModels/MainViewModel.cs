using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Timers;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core.Models;
using Core.Services;
using Microsoft.Win32;
using Wpf.Controls;
using Wpf.Services;
using Wpf.Views;
using Task = Core.Interfaces.Task;
using Timer = System.Timers.Timer;

namespace Wpf.ViewModels;

/// <summary>
/// Главная ViewModel приложения.
/// Управляет состоянием проекта, командами меню и взаимодействием с сервисами.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    #region Services

    private readonly FileService _fileService;
    private readonly ResourceService _resourceService;
    
    public Func<string?, bool>? ExportToPdfAction { get; set; }
    public Func<string?, bool>? PrintAction { get; set; }

    #endregion

    #region Private Fields

    private TaskHierarchyBuilder? _hierarchyBuilder;
    private bool _isSyncingSelection;
    private Timer? _flatListUpdateTimer;
    private const int DebounceDelayMs = 50;

    #endregion

    #region Observable Properties
    
    // Для ресурсов (прокси к ResourceService)
    public IEnumerable<Resource> Resources => ResourceService?.Resources ?? Enumerable.Empty<Resource>();
    
    [ObservableProperty]
    private string _projectName = "Новый проект";
    
    /// <summary>
    /// Сервис расчёта вовлечённости.
    /// </summary>
    [ObservableProperty]
    private EngagementCalculationService? _engagementService;

    public double ColumnWidth => 30.0 * ZoomLevel / 100.0;

    partial void OnZoomLevelChanged(int value)
    {
        // Уведомляем об изменении ColumnWidth
        OnPropertyChanged(nameof(ColumnWidth));
    }
    
    /// <summary>
    /// Менеджер проекта (содержит все задачи).
    /// </summary>
    [ObservableProperty]
    private ProjectManager? _projectManager;

    partial void OnProjectManagerChanged(ProjectManager? value)
    {
        if (value != null)
        {
            InitializeHierarchyBuilder();
            RebuildHierarchy();
            
            // Связываем с EngagementService
            if (EngagementService != null)
            {
                EngagementService.ProjectManager = value;
            }
        }
    }

    /// <summary>
    /// Путь к текущему файлу проекта.
    /// </summary>
    [ObservableProperty]
    private string? _currentFilePath;

    /// <summary>
    /// Заголовок окна.
    /// </summary>
    [ObservableProperty]
    private string _windowTitle = "Gantt Chart - Новый проект";

    /// <summary>
    /// Текст статуса в StatusBar.
    /// </summary>
    [ObservableProperty]
    private string _statusText = "Готов";

    /// <summary>
    /// Текущий масштаб отображения (%).
    /// </summary>
    [ObservableProperty]
    private int _zoomLevel = 100;
    

    /// <summary>
    /// Выбранная задача (Core.Task для GanttChart).
    /// </summary>
    [ObservableProperty]
    private Task? _selectedTask;

    partial void OnSelectedTaskChanged(Task? value)
    {
        if (_isSyncingSelection) return;

        _isSyncingSelection = true;
        try
        {
            if (value != null && _hierarchyBuilder != null && RootTasks != null)
            {
                var vm = _hierarchyBuilder.FindByTaskId(RootTasks, value.Id);
                if (vm != null && vm != SelectedTaskItem)
                {
                    SelectedTaskItem = vm;
                }
            }
            else if (value == null)
            {
                SelectedTaskItem = null;
            }
        }
        finally
        {
            _isSyncingSelection = false;
        }
    }

    /// <summary>
    /// Флаг наличия несохранённых изменений.
    /// </summary>
    [ObservableProperty]
    private bool _hasUnsavedChanges;

    /// <summary>
    /// Количество задач в проекте.
    /// </summary>
    [ObservableProperty]
    private int _taskCount;

    #endregion

    #region Sidebar Properties

    /// <summary>
    /// Корневые элементы для TreeView (иерархическая структура).
    /// </summary>
    [ObservableProperty] private ObservableCollection<TaskItemViewModel>? _rootTasks;

    /// <summary>
    /// Плоский список видимых задач для DataGrid.
    /// </summary>
    [ObservableProperty] private ObservableCollection<TaskItemViewModel>? _flatTasks;

    /// <summary>
    /// Выбранный элемент в TreeView/DataGrid (wrapper).
    /// </summary>
    [ObservableProperty] private TaskItemViewModel? _selectedTaskItem;

    partial void OnSelectedTaskItemChanged(TaskItemViewModel? value)
    {
        if (_isSyncingSelection) return;

        _isSyncingSelection = true;
        try
        {
            // Снимаем выделение с предыдущего
            if (FlatTasks != null)
            {
                foreach (var item in FlatTasks)
                {
                    if (item != value)
                        item.IsSelected = false;
                }
            }

            // Устанавливаем выделение
            if (value != null)
            {
                value.IsSelected = true;
                SelectedTask = value.Task;
            }
            else
            {
                SelectedTask = null;
            }

            // Обновляем доступные группы и состояния команд
            UpdateAvailableGroups();
            NotifyCommandStatesChanged();
        }
        finally
        {
            _isSyncingSelection = false;
        }
    }

    /// <summary>
    /// Глобальный toggle для отображения split-частей.
    /// </summary>
    [ObservableProperty] private bool _showAllSplitParts;

    partial void OnShowAllSplitPartsChanged(bool value)
    {
        // Сохраняем в настройки
        SettingsService.ShowAllSplitParts = value;

        // Применяем ко всем split-задачам
        if (RootTasks != null)
        {
            ApplyShowPartsToAll(RootTasks, value);
            ScheduleFlatListUpdate();
        }
    }

    /// <summary>
    /// Список доступных групп для подменю "Добавить в группу".
    /// </summary>
    public ObservableCollection<TaskItemViewModel> AvailableGroups { get; } = new();

    /// <summary>
    /// Можно ли превратить выбранную задачу в группу.
    /// </summary>
    public bool CanMakeGroup => SelectedTaskItem != null
                                && !SelectedTaskItem.IsGroup
                                && !SelectedTaskItem.IsPart;

    /// <summary>
    /// Можно ли убрать выбранную задачу из группы.
    /// </summary>
    public bool CanRemoveFromGroup => SelectedTaskItem?.Parent != null;

    /// <summary>
    /// Можно ли разгруппировать выбранную задачу.
    /// </summary>
    public bool CanUngroup => SelectedTaskItem != null
                              && SelectedTaskItem.IsGroup
                              && SelectedTaskItem.Children.Count > 0;

    /// <summary>
    /// Можно ли добавить подзадачу.
    /// </summary>
    public bool CanAddSubtask => SelectedTaskItem != null && !SelectedTaskItem.IsPart;

    /// <summary>
    /// Можно ли удалить выбранную задачу.
    /// </summary>
    public bool CanDeleteTask => SelectedTaskItem != null;

    #endregion

    #region Constructor

    public MainViewModel(
        FileService fileService, 
        ResourceService resourceService,
        EngagementCalculationService engagementService)
    {
        _fileService = fileService;
        _resourceService = resourceService;
        _engagementService = engagementService;  // ← сохраняем

        // Связываем FileService с ResourceService
        _fileService.ResourceService = _resourceService;

        // Инициализация коллекций
        RootTasks = new ObservableCollection<TaskItemViewModel>();
        FlatTasks = new ObservableCollection<TaskItemViewModel>();

        // Загружаем настройку ShowAllSplitParts
        _showAllSplitParts = SettingsService.ShowAllSplitParts;

        // Настройка таймера для дебаунса
        _flatListUpdateTimer = new Timer(DebounceDelayMs);
        _flatListUpdateTimer.AutoReset = false;
        _flatListUpdateTimer.Elapsed += OnFlatListUpdateTimerElapsed;

        // Создаём новый проект по умолчанию
        CreateNewProject();
    }

    #endregion

    #region File Commands

    /// <summary>
    /// Команда: Новый проект.
    /// </summary>
    [RelayCommand]
    private void NewProject()
    {
        if (HasUnsavedChanges)
        {
            var result = MessageBox.Show(
                "Сохранить изменения перед созданием нового проекта?",
                "Несохранённые изменения",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
                return;

            if (result == MessageBoxResult.Yes)
                SaveProject();
        }

        CreateNewProject();
        StatusText = "Создан новый проект";
    }

    /// <summary>
    /// Команда: Открыть проект.
    /// </summary>
    [RelayCommand]
    private void OpenProject()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Gantt Chart Files (*.gantt)|*.gantt|JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            Title = "Открыть проект"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                ProjectManager = _fileService.Load(dialog.FileName);
                CurrentFilePath = dialog.FileName;
                UpdateWindowTitle();
                UpdateTaskCount();
                HasUnsavedChanges = false;

                // Сохраняем путь к последнему файлу
                SettingsService.LastOpenedFile = dialog.FileName;

                StatusText = $"Загружен проект: {System.IO.Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка загрузки файла:\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// Команда: Сохранить проект.
    /// </summary>
    [RelayCommand]
    private void SaveProject()
    {
        if (string.IsNullOrEmpty(CurrentFilePath))
        {
            SaveProjectAs();
            return;
        }

        SaveToFile(CurrentFilePath);
    }

    /// <summary>
    /// Команда: Сохранить проект как...
    /// </summary>
    [RelayCommand]
    private void SaveProjectAs()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Gantt Chart Files (*.gantt)|*.gantt|JSON Files (*.json)|*.json",
            Title = "Сохранить проект как",
            DefaultExt = ".gantt"
        };

        if (dialog.ShowDialog() == true)
        {
            SaveToFile(dialog.FileName);
            CurrentFilePath = dialog.FileName;
            UpdateWindowTitle();

            // Сохраняем путь к последнему файлу
            SettingsService.LastOpenedFile = dialog.FileName;
        }
    }

    /// <summary>
    /// Команда: Выход из приложения.
    /// </summary>
    [RelayCommand]
    private void ExitApplication()
    {
        if (HasUnsavedChanges)
        {
            var result = MessageBox.Show(
                "Сохранить изменения перед выходом?",
                "Несохранённые изменения",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
                return;

            if (result == MessageBoxResult.Yes)
                SaveProject();
        }

        Application.Current.Shutdown();
    }

    #endregion

    #region View Commands

    /// <summary>
    /// Команда: Увеличить масштаб.
    /// </summary>
    [RelayCommand]
    private void ZoomIn()
    {
        if (ZoomLevel < 200)
        {
            ZoomLevel += 10;
            StatusText = $"Масштаб: {ZoomLevel}%";
        }
    }

    /// <summary>
    /// Команда: Уменьшить масштаб.
    /// </summary>
    [RelayCommand]
    private void ZoomOut()
    {
        if (ZoomLevel > 50)
        {
            ZoomLevel -= 10;
            StatusText = $"Масштаб: {ZoomLevel}%";
        }
    }

    /// <summary>
    /// Команда: Сбросить масштаб.
    /// </summary>
    [RelayCommand]
    private void ZoomReset()
    {
        ZoomLevel = 100;
        StatusText = "Масштаб: 100%";
    }

    /// <summary>
    /// Команда: Развернуть все группы.
    /// </summary>
    [RelayCommand]
    private void ExpandAll()
    {
        if (RootTasks != null)
        {
            SetExpandedStateRecursive(RootTasks, true);
            ScheduleFlatListUpdate();
        }
    }

    /// <summary>
    /// Команда: Свернуть все группы.
    /// </summary>
    [RelayCommand]
    private void CollapseAll()
    {
        if (RootTasks != null)
        {
            SetExpandedStateRecursive(RootTasks, false);
            ScheduleFlatListUpdate();
        }
    }

    #endregion

    #region Resource Commands

    /// <summary>
    /// Команда: Управление ресурсами.
    /// </summary>
    [RelayCommand]
    private void ManageResources()
    {
        var projectStart = ProjectManager?.Start ?? DateTime.Today;
        var dialog = new ResourceManagerDialog(_resourceService, projectStart)
        {
            Owner = Application.Current.MainWindow
        };

        dialog.ShowDialog();

        // Перерисовываем диаграмму для обновления инициалов
        StatusText = $"Ресурсов: {_resourceService.ResourceCount}";
        OnPropertyChanged(nameof(Resources));
    }

    /// <summary>
    /// Команда: Назначить ресурсы на выбранную задачу.
    /// </summary>
    [RelayCommand]
    private void AssignResources()
    {
        if (SelectedTask == null)
        {
            StatusText = "Выберите задачу для назначения ресурсов";
            return;
        }

        if (_resourceService.ResourceCount == 0)
        {
            var result = MessageBox.Show(
                "Нет доступных ресурсов. Открыть управление ресурсами?",
                "Нет ресурсов",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                ManageResources();
            }

            return;
        }

        var dialog = new AssignResourceDialog(_resourceService, SelectedTask)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true && dialog.HasChanges)
        {
            MarkAsModified();

            // Принудительная перерисовка диаграммы
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.RefreshChart();
            }

            // Уведомляем UI о необходимости перерисовки
            OnPropertyChanged(nameof(ResourceService));

            StatusText = $"Ресурсы назначены на '{SelectedTask.Name}'";
        }
    }

    #endregion

    #region Help Commands

    /// <summary>
    /// Команда: О программе.
    /// </summary>
    [RelayCommand]
    private void ShowAbout()
    {
        MessageBox.Show(
            "Gantt Chart Application\n\nВерсия: 1.0.0\nПлатформа: WPF / .NET 10\n\n© 2025",
            "О программе",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    #endregion

    #region Print Commands

    [RelayCommand]
    private void ExportToPdf()
    {
        ExportToPdfAction?.Invoke(ProjectName);
    }

    [RelayCommand]
    private void Print()
    {
        PrintAction?.Invoke(ProjectName);
    }

    #endregion

    #region Task Commands

    /// <summary>
    /// Команда: Добавить задачу.
    /// Создаёт задачу рядом с выбранной (sibling) или в корне.
    /// </summary>
    [RelayCommand]
    private void AddTask()
    {
        if (ProjectManager == null) return;

        var newTask = new Task { Name = "Новая задача" };
        ProjectManager.Add(newTask);
        ProjectManager.SetStart(newTask, TimeSpan.Zero);
        ProjectManager.SetDuration(newTask, TimeSpan.FromDays(5));

        // Если выбрана задача — добавляем как sibling (в ту же группу)
        if (SelectedTaskItem != null)
        {
            var parentGroup = ProjectManager.DirectGroupOf(SelectedTaskItem.Task);
            ProjectManager.Group(parentGroup, newTask);
            // Если родителя нет — задача остаётся в корне

            // Устанавливаем начало как у выбранной задачи
            newTask.Start = SelectedTaskItem.Task.Start;
        }

        RebuildHierarchy();
        SelectTaskById(newTask.Id);

        MarkAsModified();
        StatusText = "Добавлена новая задача";
    }

    /// <summary>
    /// Команда: Добавить подзадачу.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddSubtask))]
    private void AddSubtask()
    {
        if (ProjectManager == null || SelectedTaskItem == null) return;

        var newTask = new Task { Name = "Новая подзадача" };
        ProjectManager.Add(newTask);
        ProjectManager.SetStart(newTask, SelectedTaskItem.Task.Start);
        ProjectManager.SetDuration(newTask, TimeSpan.FromDays(3));

        // Добавляем как child к выбранной задаче
        ProjectManager.Group(SelectedTaskItem.Task, newTask);

        RebuildHierarchy();
        SelectTaskById(newTask.Id);

        MarkAsModified();
        StatusText = "Добавлена подзадача";
    }

    /// <summary>
    /// Команда: Удалить задачу.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDeleteTask))]
    private void DeleteTask()
    {
        if (ProjectManager == null || SelectedTaskItem == null) return;

        var taskToDelete = SelectedTaskItem.Task;
        var taskName = taskToDelete.Name;

        // Подтверждение удаления группы с детьми
        if (SelectedTaskItem.IsGroup && SelectedTaskItem.Children.Count > 0)
        {
            var result = MessageBox.Show(
                $"Удалить группу '{taskName}' вместе со всеми подзадачами ({SelectedTaskItem.Children.Count})?",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;
        }

        // Собираем все задачи для удаления (рекурсивно)
        var tasksToDelete = new List<Task>();
        CollectTasksToDelete(taskToDelete, tasksToDelete);

        SelectedTaskItem = null;
        SelectedTask = null;

        // Удаляем все задачи в обратном порядке (сначала детей)
        for (int i = tasksToDelete.Count - 1; i >= 0; i--)
        {
            var task = tasksToDelete[i];
            _resourceService.UnassignAllFromTask(task.Id);
            ProjectManager.Delete(task);
        }

        // Диагностика
        foreach (var t in ProjectManager.Tasks)
        {
            System.Diagnostics.Debug.WriteLine($"  [{ProjectManager.IndexOf(t)}] {t.Name}");
        }
        
        RebuildHierarchy();
        
        // Диагностика
        foreach (var t in ProjectManager.Tasks)
        {
            System.Diagnostics.Debug.WriteLine($"  [{ProjectManager.IndexOf(t)}] {t.Name}");
        }
        MarkAsModified();

        var count = tasksToDelete.Count;
        StatusText = count > 1
            ? $"Удалено задач: {count}"
            : $"Удалена задача: {taskName}";
    }

    /// <summary>
    /// Рекурсивно собирает задачу и всех её потомков для удаления.
    /// </summary>
    private void CollectTasksToDelete(Task task, List<Task> result)
    {
        // Сначала добавляем саму задачу
        result.Add(task);

        // Затем рекурсивно добавляем всех членов группы
        if (ProjectManager != null && ProjectManager.IsGroup(task))
        {
            var members = ProjectManager.MembersOf(task).ToList();
            foreach (var member in members)
            {
                CollectTasksToDelete(member, result);
            }
        }

        // Также добавляем split-части, если есть
        if (ProjectManager != null && ProjectManager.IsSplit(task))
        {
            var parts = ProjectManager.PartsOf(task).ToList();
            foreach (var part in parts)
            {
                if (!result.Contains(part))
                {
                    result.Add(part);
                }
            }
        }
    }

    /// <summary>
    /// Команда: Создать группу (обернуть выбранную задачу в новую группу).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanMakeGroup))]
    private void MakeGroup()
    {
        if (ProjectManager == null || SelectedTaskItem == null) return;

        var taskToWrap = SelectedTaskItem.Task;

        // Запоминаем текущую родительскую группу
        var currentParent = ProjectManager.DirectGroupOf(taskToWrap);

        // Создаём новую группу
        var newGroup = new Task { Name = "Новая группа" };
        ProjectManager.Add(newGroup);
        ProjectManager.SetStart(newGroup, taskToWrap.Start);
        ProjectManager.SetDuration(newGroup, TimeSpan.FromDays(1));

        // Если задача была в группе — добавляем новую группу туда же
        if (currentParent != null)
        {
            ProjectManager.Ungroup(currentParent, taskToWrap);
            ProjectManager.Group(currentParent, newGroup);
        }

        // Помещаем задачу в новую группу
        ProjectManager.Group(newGroup, taskToWrap);

        RebuildHierarchy();
        SelectTaskById(newGroup.Id);

        MarkAsModified();
        StatusText = "Создана новая группа";
    }

    /// <summary>
    /// Команда: Добавить в группу.
    /// </summary>
    [RelayCommand]
    private void AddToGroup(TaskItemViewModel? targetGroup)
    {
        if (ProjectManager == null || SelectedTaskItem == null || targetGroup == null) return;

        var taskToMove = SelectedTaskItem.Task;
        var currentParent = ProjectManager.DirectGroupOf(taskToMove);

        // Убираем из текущей группы
        if (currentParent != null)
        {
            ProjectManager.Ungroup(currentParent, taskToMove);
        }

        // Добавляем в целевую группу
        ProjectManager.Group(targetGroup.Task, taskToMove);

        RebuildHierarchy();
        SelectTaskById(taskToMove.Id);

        MarkAsModified();
        StatusText = $"Задача перемещена в '{targetGroup.Name}'";
    }

    /// <summary>
    /// Команда: Убрать из группы.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRemoveFromGroup))]
    private void RemoveFromGroup()
    {
        if (ProjectManager == null || SelectedTaskItem == null) return;

        var task = SelectedTaskItem.Task;
        var parent = ProjectManager.DirectGroupOf(task);

        if (parent != null)
        {
            ProjectManager.Ungroup(parent, task);

            RebuildHierarchy();
            SelectTaskById(task.Id);

            MarkAsModified();
            StatusText = "Задача убрана из группы";
        }
    }

    /// <summary>
    /// Команда: Разгруппировать (вынести всех детей на уровень выше).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUngroup))]
    private void Ungroup()
    {
        if (ProjectManager == null || SelectedTaskItem == null) return;

        var group = SelectedTaskItem.Task;

        // Получаем родителя группы (если есть)
        var grandParent = ProjectManager.DirectGroupOf(group);

        // Получаем всех прямых детей
        var children = ProjectManager.MembersOf(group).ToList();

        foreach (var child in children)
        {
            // Убираем из группы
            ProjectManager.Ungroup(group, child);

            // Если был дед — добавляем к нему
            if (grandParent != null)
            {
                ProjectManager.Group(grandParent, child);
            }
        }

        // Удаляем пустую группу
        ProjectManager.Delete(group);
        SelectedTaskItem = null;

        RebuildHierarchy();
        MarkAsModified();
        StatusText = "Группа разгруппирована";
    }

    /// <summary>
    /// Выделяет задачу по ID после перестройки иерархии.
    /// </summary>
    private void SelectTaskById(Guid taskId)
    {
        if (_hierarchyBuilder != null && RootTasks != null)
        {
            var vm = _hierarchyBuilder.FindByTaskId(RootTasks, taskId);
            if (vm != null)
            {
                SelectedTaskItem = vm;

                // Разворачиваем родителей для видимости
                var parent = vm.Parent;
                while (parent != null)
                {
                    parent.IsExpanded = true;
                    parent = parent.Parent;
                }
            }
        }
    }

    #endregion

    #region Public Methods
    
    /// <summary>
    /// Обрабатывает завершение drag-операции.
    /// Синхронизирует sidebar и помечает проект как изменённый.
    /// </summary>
    public void OnTaskDragged(TaskDragEventArgs e)
    {
        // Обновляем TaskItemViewModel
        if (_hierarchyBuilder != null && RootTasks != null)
        {
            var taskVm = _hierarchyBuilder.FindByTaskId(RootTasks, e.Task.Id);
            taskVm?.Refresh();
        }

        // Для Reordering нужно перестроить иерархию
        if (e.Operation == DragOperation.Reordering && e.OldIndex != e.NewIndex)
        {
            RebuildHierarchy();
        }

        MarkAsModified();

        StatusText = e.Operation switch
        {
            DragOperation.Moving => $"Задача '{e.Task.Name}' перемещена",
            DragOperation.ResizingStart => $"Изменено начало '{e.Task.Name}'",
            DragOperation.ResizingEnd => $"Изменена длительность '{e.Task.Name}'",
            DragOperation.Reordering => $"Задача '{e.Task.Name}' переупорядочена",
            DragOperation.ProgressAdjusting => $"Прогресс '{e.Task.Name}': {(int)(e.NewComplete * 100)}%",
            _ => "Готов"
        };
    }
    
    /// <summary>
    /// Загружает последний открытый файл (вызывается при старте).
    /// </summary>
    public void LoadLastOpenedFile()
    {
        var lastFile = SettingsService.LastOpenedFile;
        if (!string.IsNullOrEmpty(lastFile) && System.IO.File.Exists(lastFile))
        {
            try
            {
                ProjectManager = _fileService.Load(lastFile);
                CurrentFilePath = lastFile;
                UpdateWindowTitle();
                UpdateTaskCount();
                HasUnsavedChanges = false;
                StatusText = $"Загружен последний проект: {System.IO.Path.GetFileName(lastFile)}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки последнего файла: {ex.Message}");
                CreateNewProject();
            }
        }
    }

    /// <summary>
    /// Помечает проект как изменённый.
    /// </summary>
    public void MarkAsModified()
    {
        HasUnsavedChanges = true;
        UpdateWindowTitle();
    }

    /// <summary>
    /// Обновляет TaskItemViewModel после изменения задачи на диаграмме.
    /// </summary>
    public void OnTaskModified(Task task)
    {
        if (_hierarchyBuilder != null && RootTasks != null)
        {
            var vm = _hierarchyBuilder.FindByTaskId(RootTasks, task.Id);
            vm?.Refresh();
        }

        MarkAsModified();
    }

    /// <summary>
    /// Полная перестройка иерархии (после добавления/удаления задач).
    /// </summary>
    public void RefreshHierarchy()
    {
        RebuildHierarchy();
        UpdateTaskCount();
    }

    #endregion

    #region Private Methods

    private void CreateNewProject()
    {
        ProjectManager = new ProjectManager
        {
            Start = DateTime.Today
        };

        CurrentFilePath = null;
        HasUnsavedChanges = false;
        TaskCount = 0;
        UpdateWindowTitle();

        // Очищаем ресурсы
        _resourceService.Clear();
    }

    /// <summary>
    /// Создаёт демонстрационный проект с тестовыми данными.
    /// </summary>
    [RelayCommand]
    private void CreateDemoProject()
    {
        CreateNewProject();

        if (ProjectManager == null)
            return;

        // Фаза 1: Планирование
        var phase1 = new Task { Name = "Фаза 1: Планирование" };
        ProjectManager.Add(phase1);
        ProjectManager.SetStart(phase1, TimeSpan.FromDays(0));
        ProjectManager.SetDuration(phase1, TimeSpan.FromDays(1));

        var task1 = new Task { Name = "Анализ требований" };
        ProjectManager.Add(task1);
        ProjectManager.SetStart(task1, TimeSpan.FromDays(0));
        ProjectManager.SetDuration(task1, TimeSpan.FromDays(5));
        ProjectManager.SetComplete(task1, 1.0f);
        ProjectManager.Group(phase1, task1);

        var task2 = new Task { Name = "Техническое задание" };
        ProjectManager.Add(task2);
        ProjectManager.SetStart(task2, TimeSpan.FromDays(5));
        ProjectManager.SetDuration(task2, TimeSpan.FromDays(3));
        ProjectManager.SetComplete(task2, 0.8f);
        ProjectManager.Group(phase1, task2);
        ProjectManager.Relate(task1, task2);

        // Фаза 2: Разработка
        var phase2 = new Task { Name = "Фаза 2: Разработка" };
        ProjectManager.Add(phase2);
        ProjectManager.SetStart(phase2, TimeSpan.FromDays(8));
        ProjectManager.SetDuration(phase2, TimeSpan.FromDays(1));

        var task3 = new Task { Name = "Разработка Core" };
        ProjectManager.Add(task3);
        ProjectManager.SetStart(task3, TimeSpan.FromDays(8));
        ProjectManager.SetDuration(task3, TimeSpan.FromDays(10));
        ProjectManager.SetComplete(task3, 0.5f);
        ProjectManager.Group(phase2, task3);
        ProjectManager.Relate(task2, task3);

        var task4 = new Task { Name = "Разработка UI" };
        ProjectManager.Add(task4);
        ProjectManager.SetStart(task4, TimeSpan.FromDays(12));
        ProjectManager.SetDuration(task4, TimeSpan.FromDays(8));
        ProjectManager.SetComplete(task4, 0.3f);
        ProjectManager.Group(phase2, task4);

        var task5 = new Task { Name = "Интеграция" };
        ProjectManager.Add(task5);
        ProjectManager.SetStart(task5, TimeSpan.FromDays(18));
        ProjectManager.SetDuration(task5, TimeSpan.FromDays(5));
        ProjectManager.SetComplete(task5, 0.0f);
        ProjectManager.Group(phase2, task5);
        ProjectManager.Relate(task3, task5);
        ProjectManager.Relate(task4, task5);

        // Фаза 3: Тестирование
        var phase3 = new Task { Name = "Фаза 3: Тестирование" };
        ProjectManager.Add(phase3);
        ProjectManager.SetStart(phase3, TimeSpan.FromDays(23));
        ProjectManager.SetDuration(phase3, TimeSpan.FromDays(1));

        var task6 = new Task { Name = "Юнит-тесты" };
        ProjectManager.Add(task6);
        ProjectManager.SetStart(task6, TimeSpan.FromDays(23));
        ProjectManager.SetDuration(task6, TimeSpan.FromDays(4));
        ProjectManager.Group(phase3, task6);
        ProjectManager.Relate(task5, task6);

        var task7 = new Task { Name = "Интеграционные тесты" };
        ProjectManager.Add(task7);
        ProjectManager.SetStart(task7, TimeSpan.FromDays(27));
        ProjectManager.SetDuration(task7, TimeSpan.FromDays(3));
        ProjectManager.Group(phase3, task7);
        ProjectManager.Relate(task6, task7);

        // Релиз
        var task8 = new Task { Name = "Релиз v1.0" };
        ProjectManager.Add(task8);
        ProjectManager.SetStart(task8, TimeSpan.FromDays(30));
        ProjectManager.SetDuration(task8, TimeSpan.FromDays(1));
        ProjectManager.Relate(task7, task8);

        // Перестраиваем иерархию
        RebuildHierarchy();

        UpdateTaskCount();
        HasUnsavedChanges = true;
        UpdateWindowTitle();
        StatusText = "Создан демо-проект с 11 задачами";
    }

    private void SaveToFile(string filePath)
    {
        if (ProjectManager == null)
            return;

        try
        {
            _fileService.Save(ProjectManager, filePath);
            HasUnsavedChanges = false;
            UpdateWindowTitle();
            StatusText = $"Сохранено: {System.IO.Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Ошибка сохранения файла:\n{ex.Message}",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void UpdateWindowTitle()
    {
        var fileName = string.IsNullOrEmpty(CurrentFilePath)
            ? "Новый проект"
            : System.IO.Path.GetFileName(CurrentFilePath);

        var modified = HasUnsavedChanges ? " *" : "";

        WindowTitle = $"Gantt Chart - {fileName}{modified}";
    }

    private void UpdateTaskCount()
    {
        TaskCount = ProjectManager?.Tasks.Count ?? 0;
    }

    #endregion

    #region Hierarchy Methods

    /// <summary>
    /// Обновляет список доступных групп для подменю.
    /// </summary>
    private void UpdateAvailableGroups()
    {
        AvailableGroups.Clear();

        if (RootTasks == null || SelectedTaskItem == null) return;

        // Собираем все группы, кроме текущей и её потомков
        CollectAvailableGroups(RootTasks, SelectedTaskItem);
    }

    private void CollectAvailableGroups(
        ObservableCollection<TaskItemViewModel> items, 
        TaskItemViewModel excludeTask)
    {
        foreach (var item in items)
        {
            // Добавляем группу, если это не сама выбранная задача и не её потомок
            if (item.IsGroup && item != excludeTask && !IsDescendantOf(item, excludeTask))
            {
                AvailableGroups.Add(item);
            }

            // Рекурсивно проверяем детей
            if (item.Children.Count > 0)
            {
                CollectAvailableGroups(item.Children, excludeTask);
            }
        }
    }

    /// <summary>
    /// Проверяет, является ли item потомком possibleAncestor.
    /// </summary>
    private bool IsDescendantOf(TaskItemViewModel item, TaskItemViewModel possibleAncestor)
    {
        var current = item.Parent;
        while (current != null)
        {
            if (current == possibleAncestor)
                return true;
            current = current.Parent;
        }
        return false;
    }

    /// <summary>
    /// Уведомляет об изменении состояния команд.
    /// </summary>
    private void NotifyCommandStatesChanged()
    {
        OnPropertyChanged(nameof(CanMakeGroup));
        OnPropertyChanged(nameof(CanRemoveFromGroup));
        OnPropertyChanged(nameof(CanUngroup));
        OnPropertyChanged(nameof(CanAddSubtask));
        OnPropertyChanged(nameof(CanDeleteTask));
        OnPropertyChanged(nameof(CanEditNote));
    }
    
    private void InitializeHierarchyBuilder()
    {
        if (ProjectManager == null) return;

        _hierarchyBuilder = new TaskHierarchyBuilder(
            ProjectManager,
            OnExpandChanged,
            OnShowPartsChanged,
            OnTaskModifiedFromSidebar);
    }
    
    private void OnTaskModifiedFromSidebar()
    {
        // Перерисовываем GanttChart
        if (Application.Current.MainWindow is MainWindow mainWindow)
        {
            mainWindow.RefreshChart();
        }

        // Помечаем как изменённый
        MarkAsModified();
    }

    private void RebuildHierarchy()
    {
        if (_hierarchyBuilder == null || ProjectManager == null) return;

        RootTasks = _hierarchyBuilder.BuildHierarchy();

        // Применяем глобальную настройку ShowAllSplitParts
        if (ShowAllSplitParts)
        {
            ApplyShowPartsToAll(RootTasks, true);
        }

        UpdateFlatList();
        UpdateTaskCount();

        // Перерисовываем GanttChart
        RefreshGanttChart();
    }
    
    /// <summary>
    /// Перерисовывает GanttChart.
    /// </summary>
    private void RefreshGanttChart()
    {
        if (Application.Current?.MainWindow is MainWindow mainWindow)
        {
            // Принудительный сброс и перерисовка
            mainWindow.ForceRefreshChart();
        }
    }

    private void UpdateFlatList()
    {
        if (_hierarchyBuilder == null || RootTasks == null) return;

        FlatTasks = _hierarchyBuilder.BuildFlatList(RootTasks);
    }

    private void OnExpandChanged()
    {
        ScheduleFlatListUpdate();
    }

    private void OnShowPartsChanged(TaskItemViewModel splitVm)
    {
        _hierarchyBuilder?.RebuildSplitChildren(splitVm);
        ScheduleFlatListUpdate();
    }

    private void ScheduleFlatListUpdate()
    {
        _flatListUpdateTimer?.Stop();
        _flatListUpdateTimer?.Start();
    }

    private void OnFlatListUpdateTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        Application.Current?.Dispatcher.Invoke(UpdateFlatList);
    }

    private void SetExpandedStateRecursive(ObservableCollection<TaskItemViewModel> items, bool expanded)
    {
        foreach (var item in items)
        {
            if (item.IsGroup)
            {
                item.IsExpanded = expanded;
            }
            if (item.Children.Count > 0)
            {
                SetExpandedStateRecursive(item.Children, expanded);
            }
        }
    }

    private void ApplyShowPartsToAll(ObservableCollection<TaskItemViewModel> items, bool showParts)
    {
        foreach (var item in items)
        {
            if (item.IsSplitRoot)
            {
                item.ShowParts = showParts;
            }
            if (item.Children.Count > 0)
            {
                ApplyShowPartsToAll(item.Children, showParts);
            }
        }
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Сервис ресурсов (для binding).
    /// </summary>
    public ResourceService ResourceService => _resourceService;

    #endregion
    
    #region Note Commands

    /// <summary>
    /// Действие для редактирования заметки (связывается с GanttChartControl).
    /// </summary>
    public Action<Task?>? EditNoteAction { get; set; }

    /// <summary>
    /// Можно ли редактировать заметку.
    /// </summary>
    public bool CanEditNote => SelectedTask != null;

    /// <summary>
    /// Команда: Редактировать/добавить заметку.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanEditNote))]
    private void EditNote()
    {
        if (SelectedTask == null)
        {
            StatusText = "Выберите задачу для редактирования заметки";
            return;
        }

        // Вызываем через Action, который связан с GanttChartControl
        EditNoteAction?.Invoke(SelectedTask);
    
        StatusText = string.IsNullOrEmpty(SelectedTask.Note) 
            ? $"Добавление заметки к '{SelectedTask.Name}'"
            : $"Редактирование заметки '{SelectedTask.Name}'";
    }

    #endregion
}