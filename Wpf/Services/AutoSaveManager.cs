using System;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Core.Services;
using Wpf.ViewModels;

namespace Wpf.Services;

/// <summary>
/// Сервис для управления автоматическим сохранением проекта.
/// Выполняет сохранение каждые 5 минут, если есть несохранённые изменения и файл проекта существует.
/// </summary>
public class AutoSaveManager : ObservableObject
{
    #region Fields

    private readonly FileService _fileService;
    private readonly MainViewModel _mainViewModel;
    private readonly DispatcherTimer _autoSaveTimer;
    private bool _isEnabled = true;

    #endregion

    #region Properties

    /// <summary>
    /// Флаг активации автосохранения.
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    /// <summary>
    /// Интервал автосохранения в минутах (по умолчанию 5).
    /// </summary>
    public int AutoSaveIntervalMinutes { get; set; } = 5;

    #endregion

    #region Constructor

    /// <summary>
    /// Конструктор.
    /// </summary>
    /// <param name="fileService">Сервис для сохранения файлов.</param>
    /// <param name="mainViewModel">Главная ViewModel для доступа к состоянию проекта.</param>
    public AutoSaveManager(FileService fileService, MainViewModel mainViewModel)
    {
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _mainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));

        // Инициализация таймера
        _autoSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(AutoSaveIntervalMinutes)
        };
        _autoSaveTimer.Tick += OnAutoSaveTick;

        // Подписка на события изменений для установки HasUnsavedChanges
        SubscribeToChangeEvents();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Запускает автосохранение.
    /// </summary>
    public void StartAutoSave()
    {
        if (IsEnabled)
        {
            _autoSaveTimer.Start();
        }
    }

    /// <summary>
    /// Останавливает автосохранение.
    /// </summary>
    public void StopAutoSave()
    {
        _autoSaveTimer.Stop();
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Обработчик события таймера.
    /// Проверяет наличие изменений и сохраняет проект, если нужно.
    /// </summary>
    private void OnAutoSaveTick(object? sender, EventArgs e)
    {
        if (!_mainViewModel.HasUnsavedChanges || string.IsNullOrEmpty(_mainViewModel.CurrentFilePath) || _mainViewModel.ProjectManager == null)
        {
            return;
        }

        try
        {
            // Тихое сохранение без UI-диалогов
            _fileService.Save(_mainViewModel.ProjectManager, _mainViewModel.CurrentFilePath);
            _mainViewModel.HasUnsavedChanges = false;
            _mainViewModel.StatusText = "Автосохранение завершено";
        }
        catch (Exception ex)
        {
            // Логируем ошибку, но не показываем UI
            System.Diagnostics.Debug.WriteLine($"Ошибка автосохранения: {ex.Message}");
            _mainViewModel.StatusText = "Ошибка автосохранения";
        }
    }

    /// <summary>
    /// Подписывается на события изменений в проекте и ресурсах.
    /// </summary>
    private void SubscribeToChangeEvents()
    {
        if (_mainViewModel.ProjectManager != null)
        {
            _mainViewModel.ProjectManager.ScheduleChanged += OnProjectChanged;
        }

        _mainViewModel.ResourceService.ResourcesChanged += OnResourcesChanged;
        _mainViewModel.ResourceService.AssignmentsChanged += OnResourcesChanged;
        _mainViewModel.ResourceService.ParticipationIntervalsChanged += OnResourcesChanged;
        _mainViewModel.ResourceService.AbsencesChanged += OnResourcesChanged;

        // Подписка на изменение ProjectManager (если он изменится)
        _mainViewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.ProjectManager))
            {
                if (_mainViewModel.ProjectManager != null)
                {
                    _mainViewModel.ProjectManager.ScheduleChanged += OnProjectChanged;
                }
            }
        };
    }

    /// <summary>
    /// Обработчик изменений в проекте.
    /// </summary>
    private void OnProjectChanged(object? sender, EventArgs e)
    {
        _mainViewModel.HasUnsavedChanges = true;
    }

    /// <summary>
    /// Обработчик изменений в ресурсах.
    /// </summary>
    private void OnResourcesChanged(object? sender, EventArgs e)
    {
        _mainViewModel.HasUnsavedChanges = true;
    }

    #endregion
}