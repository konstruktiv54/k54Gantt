using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core.Services;
using Microsoft.Win32;

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

    #region Observable Properties

    /// <summary>
    /// Менеджер проекта (содержит все задачи).
    /// </summary>
    [ObservableProperty]
    private ProjectManager? _projectManager;

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
    /// Выбранная задача.
    /// </summary>
    [ObservableProperty]
    private Task? _selectedTask;

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

    #region Constructor

    public MainViewModel(FileService fileService, ResourceService resourceService)
    {
        _fileService = fileService;
        _resourceService = resourceService;

        // Связываем FileService с ResourceService
        _fileService.ResourceService = _resourceService;

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

    #endregion

    #region Resource Commands

    /// <summary>
    /// Команда: Управление ресурсами.
    /// </summary>
    [RelayCommand]
    private void ManageResources()
    {
        // TODO: Открыть диалог управления ресурсами
        StatusText = "Управление ресурсами (в разработке)";
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
            : System.IO.Path.GetFileName((string?)CurrentFilePath);

        var modified = HasUnsavedChanges ? " *" : "";

        WindowTitle = $"Gantt Chart - {fileName}{modified}";
    }

    private void UpdateTaskCount()
    {
        TaskCount = ProjectManager?.Tasks.Count ?? 0;
    }

    #endregion
}