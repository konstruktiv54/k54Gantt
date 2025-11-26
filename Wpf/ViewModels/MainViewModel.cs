using System.Collections.ObjectModel;
using System.Timers;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core.Services;
using Microsoft.Win32;
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

    #endregion

    #region Private Fields

    private TaskHierarchyBuilder? _hierarchyBuilder;
    private bool _isSyncingSelection;
    private Timer? _flatListUpdateTimer;
    private const int DebounceDelayMs = 50;

    #endregion

    #region Observable Properties

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
    [ObservableProperty]
    private ObservableCollection<TaskItemViewModel>? _rootTasks;

    /// <summary>
    /// Плоский список видимых задач для DataGrid.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<TaskItemViewModel>? _flatTasks;

    /// <summary>
    /// Выбранный элемент в TreeView/DataGrid (wrapper).
    /// </summary>
    [ObservableProperty]
    private TaskItemViewModel? _selectedTaskItem;

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
        }
        finally
        {
            _isSyncingSelection = false;
        }
    }

    /// <summary>
    /// Глобальный toggle для отображения split-частей.
    /// </summary>
    [ObservableProperty]
    private bool _showAllSplitParts;

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

    #endregion

    #region Constructor

    public MainViewModel(FileService fileService, ResourceService resourceService)
    {
        _fileService = fileService;
        _resourceService = resourceService;

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
        var dialog = new ResourceManagerDialog(_resourceService)
        {
            Owner = Application.Current.MainWindow
        };

        dialog.ShowDialog();

        // Перерисовываем диаграмму для обновления инициалов
        StatusText = $"Ресурсов: {_resourceService.ResourceCount}";
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

    #region Public Methods

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

    private void InitializeHierarchyBuilder()
    {
        if (ProjectManager == null) return;

        _hierarchyBuilder = new TaskHierarchyBuilder(
            ProjectManager,
            OnExpandChanged,
            OnShowPartsChanged);
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
}