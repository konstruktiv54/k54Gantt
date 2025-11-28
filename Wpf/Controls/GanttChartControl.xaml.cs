using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Core.Services;
using Wpf.Rendering;
using Task = Core.Interfaces.Task;

namespace Wpf.Controls;

/// <summary>
/// Пользовательский контрол для отображения диаграммы Ганта.
/// Использует Canvas-based рендеринг с несколькими слоями.
/// </summary>
public partial class GanttChartControl : UserControl
{
    #region Private Fields

    private readonly GridRenderer _gridRenderer;
    private readonly HeaderRenderer _headerRenderer;
    private readonly TaskRenderer _taskRenderer;

    private bool _isRendering;
    
    // Drag & Drop — НЕ readonly, чтобы можно было сбрасывать
    private DragState? _dragState;
    private const double EdgeZoneWidth = 8.0;
    private const double MinDurationDays = 1.0;
    private const double PixelsPerProgressPercent = 3.0;
    private const double DeadlineGrabZone = 8.0;
    private const int DefaultDeadlineOffsetDays = 5;
    private Dictionary<Guid, (TimeSpan Start, TimeSpan Duration)>? _originalChildPositions;
    
    #endregion

    #region Dependency Properties

    /// <summary>
    /// Сервис ресурсов для отображения инициалов.
    /// </summary>
    public static readonly DependencyProperty ResourceServiceProperty =
        DependencyProperty.Register(
            nameof(ResourceService),
            typeof(ResourceService),
            typeof(GanttChartControl),
            new PropertyMetadata(null, OnResourceServiceChanged));

    public ResourceService? ResourceService
    {
        get => (ResourceService?)GetValue(ResourceServiceProperty);
        set => SetValue(ResourceServiceProperty, value);
    }
    
    private static void OnResourceServiceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not GanttChartControl control) return;
        if (e.NewValue is ResourceService service)
        {
            control._taskRenderer.SetResourceService(service);
        }
        control.InvalidateChart();
    }
    
    /// <summary>
    /// Менеджер проекта с задачами.
    /// </summary>
    public static readonly DependencyProperty ProjectManagerProperty =
        DependencyProperty.Register(
            nameof(ProjectManager),
            typeof(ProjectManager),
            typeof(GanttChartControl),
            new PropertyMetadata(null, OnProjectManagerChanged));

    public ProjectManager? ProjectManager
    {
        get => (ProjectManager?)GetValue(ProjectManagerProperty);
        set => SetValue(ProjectManagerProperty, value);
    }

    /// <summary>
    /// Ширина одной колонки (день) в пикселях.
    /// </summary>
    public static readonly DependencyProperty ColumnWidthProperty =
        DependencyProperty.Register(
            nameof(ColumnWidth),
            typeof(double),
            typeof(GanttChartControl),
            new PropertyMetadata(30.0, OnRenderPropertyChanged));

    public double ColumnWidth
    {
        get => (double)GetValue(ColumnWidthProperty);
        set => SetValue(ColumnWidthProperty, value);
    }

    /// <summary>
    /// Высота бара задачи в пикселях.
    /// </summary>
    public static readonly DependencyProperty BarHeightProperty =
        DependencyProperty.Register(
            nameof(BarHeight),
            typeof(double),
            typeof(GanttChartControl),
            new PropertyMetadata(24.0, OnRenderPropertyChanged));

    public double BarHeight
    {
        get => (double)GetValue(BarHeightProperty);
        set => SetValue(BarHeightProperty, value);
    }

    /// <summary>
    /// Вертикальный отступ между барами.
    /// </summary>
    public static readonly DependencyProperty BarSpacingProperty =
        DependencyProperty.Register(
            nameof(BarSpacing),
            typeof(double),
            typeof(GanttChartControl),
            new PropertyMetadata(4.0, OnRenderPropertyChanged));

    public double BarSpacing
    {
        get => (double)GetValue(BarSpacingProperty);
        set => SetValue(BarSpacingProperty, value);
    }

    /// <summary>
    /// Высота строки (BarHeight + BarSpacing).
    /// </summary>
    public double RowHeight => BarHeight + BarSpacing;

    /// <summary>
    /// Высота заголовка.
    /// </summary>
    public static readonly DependencyProperty HeaderHeightProperty =
        DependencyProperty.Register(
            nameof(HeaderHeight),
            typeof(double),
            typeof(GanttChartControl),
            new PropertyMetadata(50.0, OnRenderPropertyChanged));

    public double HeaderHeight
    {
        get => (double)GetValue(HeaderHeightProperty);
        set => SetValue(HeaderHeightProperty, value);
    }

    /// <summary>
    /// Уровень масштабирования (50-200%).
    /// </summary>
    public static readonly DependencyProperty ZoomLevelProperty =
        DependencyProperty.Register(
            nameof(ZoomLevel),
            typeof(int),
            typeof(GanttChartControl),
            new PropertyMetadata(100, OnZoomLevelChanged));

    public int ZoomLevel
    {
        get => (int)GetValue(ZoomLevelProperty);
        set => SetValue(ZoomLevelProperty, value);
    }

    /// <summary>
    /// Выбранная задача.
    /// </summary>
    public static readonly DependencyProperty SelectedTaskProperty =
        DependencyProperty.Register(
            nameof(SelectedTask),
            typeof(Task),
            typeof(GanttChartControl),
            new PropertyMetadata(null, OnSelectedTaskChanged));

    public Task? SelectedTask
    {
        get => (Task?)GetValue(SelectedTaskProperty);
        set => SetValue(SelectedTaskProperty, value);
    }

    /// <summary>
    /// Показывать линии связей.
    /// </summary>
    public static readonly DependencyProperty ShowRelationsProperty =
        DependencyProperty.Register(
            nameof(ShowRelations),
            typeof(bool),
            typeof(GanttChartControl),
            new PropertyMetadata(true, OnRenderPropertyChanged));

    public bool ShowRelations
    {
        get => (bool)GetValue(ShowRelationsProperty);
        set => SetValue(ShowRelationsProperty, value);
    }

    /// <summary>
    /// Показывать линию "Сегодня".
    /// </summary>
    public static readonly DependencyProperty ShowTodayLineProperty =
        DependencyProperty.Register(
            nameof(ShowTodayLine),
            typeof(bool),
            typeof(GanttChartControl),
            new PropertyMetadata(true, OnRenderPropertyChanged));

    public bool ShowTodayLine
    {
        get => (bool)GetValue(ShowTodayLineProperty);
        set => SetValue(ShowTodayLineProperty, value);
    }

    /// <summary>
    /// Выделять выходные дни.
    /// </summary>
    public static readonly DependencyProperty HighlightWeekendsProperty =
        DependencyProperty.Register(
            nameof(HighlightWeekends),
            typeof(bool),
            typeof(GanttChartControl),
            new PropertyMetadata(true, OnRenderPropertyChanged));

    public bool HighlightWeekends
    {
        get => (bool)GetValue(HighlightWeekendsProperty);
        set => SetValue(HighlightWeekendsProperty, value);
    }
    
    /// <summary>
    /// Статусное сообщение для отображения в UI.
    /// </summary>
    public static readonly DependencyProperty StatusMessageProperty =
        DependencyProperty.Register(
            nameof(StatusMessage),
            typeof(string),
            typeof(GanttChartControl),
            new PropertyMetadata(string.Empty));

    public string StatusMessage
    {
        get => (string)GetValue(StatusMessageProperty);
        set => SetValue(StatusMessageProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Событие выбора задачи.
    /// </summary>
    public event EventHandler<Task?>? TaskSelected;

    /// <summary>
    /// Событие двойного клика по задаче.
    /// </summary>
    public event EventHandler<Task>? TaskDoubleClicked;
    
    /// <summary>
    /// Событие завершения drag-операции (для Undo/Redo).
    /// </summary>
    public event EventHandler<TaskDragEventArgs>? TaskDragged;
    
    /// <summary>
    /// Событие модификации задачи (для обновления UI).
    /// </summary>
    public event EventHandler<TaskDragEventArgs>? TaskModified;

    #endregion

    #region Constructor

    public GanttChartControl()
    {
        InitializeComponent();

        // Инициализация рендереров
        _gridRenderer = new GridRenderer(this);
        _headerRenderer = new HeaderRenderer(this);
        _taskRenderer = new TaskRenderer(this);
        Focusable = true;

        // Подписка на события
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    
        // ВАЖНО: Используем Preview события для кнопок мыши,
        // чтобы перехватить ДО ScrollViewer (который блокирует обычные события)
        KeyDown += OnKeyDown;
        PreviewMouseWheel += OnPreviewMouseWheel;
        PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
        MouseMove += OnMouseMove;
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewMouseDown += OnPreviewMouseDown;
        PreviewMouseUp += OnPreviewMouseUp;
    }

    #endregion

    #region Property Changed Callbacks

    private static void OnProjectManagerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GanttChartControl control)
        {
            control.InvalidateChart();
        }
    }

    private static void OnRenderPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GanttChartControl control)
        {
            control.InvalidateChart();
        }
    }

    private static void OnZoomLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GanttChartControl control)
        {
            var baseWidth = 30.0;
            control.ColumnWidth = baseWidth * control.ZoomLevel / 100.0;
        }
    }

    private static void OnSelectedTaskChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GanttChartControl control)
        {
            control.TaskSelected?.Invoke(control, e.NewValue as Task);
            control.RenderOverlay();
        }
    }

    #endregion

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        InvalidateChart();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        InvalidateChart();
    }

    private void OnChartScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        HeaderScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);
    }
    
    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            var delta = e.Delta > 0 ? 10 : -10;
            var newZoom = Math.Clamp(ZoomLevel + delta, 50, 200);
            ZoomLevel = newZoom;
            e.Handled = true;
        }
    }
    
    /// <summary>
    /// Обработчик нажатия левой кнопки мыши.
    /// </summary>
    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Focus();
        var position = e.GetPosition(TaskLayer);
        var hitResult = HitTestTaskWithIndex(position);

        if (hitResult.Task != null)
        {
            SelectedTask = hitResult.Task;

            if (e.ClickCount == 2)
            {
                TaskDoubleClicked?.Invoke(this, hitResult.Task);
                e.Handled = true;
                return;
            }

            if (!CanDragTask(hitResult.Task))
            {
                e.Handled = true;
                return;
            }

            // Определяем тип операции
            var operation = DetermineOperation(hitResult.Task, position, hitResult.RowIndex);

            if (operation != DragOperation.None)
            {
                StartDrag(hitResult.Task, position, hitResult.RowIndex, operation);
                e.Handled = true;
            }
            else
            {
                e.Handled = true;
            }
        }
        else
        {
            SelectedTask = null;
        }
    }
    
    /// <summary>
    /// Обработчик отпускания левой кнопки мыши.
    /// </summary>
    private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragState == null || !_dragState.IsActive) return;
        
        var position = e.GetPosition(TaskLayer);
        CompleteDrag(position);
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(TaskLayer);

        if (_dragState != null && _dragState.IsActive)
        {
            switch (_dragState.Operation)
            {
                case DragOperation.ProgressAdjusting:
                    if (e.MiddleButton != MouseButtonState.Pressed)
                    {
                        CancelDrag();
                        return;
                    }
                    UpdateProgressPreview(position);
                    break;
                
                case DragOperation.DeadlineMoving:
                    if (e.LeftButton != MouseButtonState.Pressed)
                    {
                        CancelDrag();
                        return;
                    }
                    UpdateDeadlinePreview(position);
                    break;

                default:
                    if (e.LeftButton != MouseButtonState.Pressed)
                    {
                        CancelDrag();
                        return;
                    }
                    UpdateDragPreview(position);
                    break;
            }
        }
        else
        {
            UpdateCursor(position);
        }
    }
    
    /// <summary>
    /// Обрабатывает нажатия клавиш.
    /// </summary>
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (SelectedTask == null) return;

        switch (e.Key)
        {
            case Key.D:
                ToggleDeadline(SelectedTask);
                e.Handled = true;
                break;

            case Key.Delete:
                if (IsDeadlineSelected(SelectedTask))
                {
                    RemoveDeadline(SelectedTask);
                    e.Handled = true;
                }
                break;

            case Key.Escape:
                if (_dragState != null && _dragState.IsActive)
                {
                    CancelDrag();
                    e.Handled = true;
                }
                break;
        }
    }
    
    /// <summary>
    /// Обновляет позицию дедлайна при перетаскивании.
    /// </summary>
    private void UpdateDeadlinePreview(Point currentPosition)
    {
        if (_dragState == null || _dragState.Task == null) return;

        var task = _dragState.Task;
        var columnWidth = ColumnWidth;

        var newDeadlineDays = (int)Math.Round(currentPosition.X / columnWidth);
        
        if (newDeadlineDays < task.Start.Days)
        {
            newDeadlineDays = (int)task.Start.Days;
        }

        var newDeadline = TimeSpan.FromDays(newDeadlineDays);

        ProjectManager?.SetDeadline(task, newDeadline);

        InvalidateChart();
    }

    
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || _dragState == null || !_dragState.IsActive) return;
        CancelDrag();
        e.Handled = true;
    }
    
    /// <summary>
    /// Обработчик нажатия кнопки мыши (для средней кнопки — изменение прогресса).
    /// </summary>
    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle)
            return;

        var position = e.GetPosition(TaskLayer);
        var task = HitTestTask(position);

        if (task != null)
        {
            if (!CanAdjustProgress(task))
            {
                e.Handled = true;
                return;
            }

            SelectedTask = task;

            StartProgressAdjust(task, position);
            e.Handled = true;
        }
    }
    
    /// <summary>
    /// Обработчик отпускания кнопки мыши (для средней кнопки).
    /// </summary>
    private void OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle)
            return;

        if (_dragState == null || !_dragState.IsActive || _dragState.Operation != DragOperation.ProgressAdjusting)
            return;

        var position = e.GetPosition(TaskLayer);
        CompleteProgressAdjust(position);
        e.Handled = true;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Перерисовывает всю диаграмму.
    /// </summary>
    public void InvalidateChart()
    {
        if (_isRendering || !IsLoaded)
            return;

        _isRendering = true;

        try
        {
            Render();
        }
        finally
        {
            _isRendering = false;
        }
    }
    
    /// <summary>
    /// Принудительно перерисовывает диаграмму.
    /// </summary>
    public void Refresh()
    {
        InvalidateChart();
    }

    /// <summary>
    /// Прокручивает диаграмму к указанной задаче.
    /// </summary>
    public void ScrollToTask(Task task)
    {
        if (ProjectManager == null)
            return;

        var index = GetRealTaskIndex(task);
        if (index < 0)
            return;

        var y = index * RowHeight;
        var x = task.Start.Days * ColumnWidth;

        ChartScrollViewer.ScrollToVerticalOffset(y - ChartScrollViewer.ViewportHeight / 2);
        ChartScrollViewer.ScrollToHorizontalOffset(x - 50);
    }

    /// <summary>
    /// Прокручивает диаграмму к сегодняшней дате.
    /// </summary>
    public void ScrollToToday()
    {
        if (ProjectManager == null)
            return;

        var todayOffset = (DateTime.Today - ProjectManager.Start).Days;
        var x = todayOffset * ColumnWidth;

        ChartScrollViewer.ScrollToHorizontalOffset(x - ChartScrollViewer.ViewportWidth / 2);
    }
    
    /// <summary>
    /// Полностью сбрасывает и перерисовывает диаграмму.
    /// </summary>
    public void ForceFullRedraw()
    {
        ClearAllLayers();
        _isRendering = false;
    
        if (IsLoaded && ProjectManager != null)
        {
            Render();
        }
    }

    #endregion

    #region Private Methods - Rendering

    private int GetRealTaskIndex(Task task)
    {
        if (ProjectManager == null) return -1;
    
        var tasks = ProjectManager.Tasks.ToList();
        return tasks.IndexOf(task);
    }
    
    private void Render()
    {
        if (ProjectManager == null)
        {
            ClearAllLayers();
            return;
        }

        var (totalWidth, totalHeight) = CalculateCanvasSize();

        SetCanvasSize(ChartCanvas, totalWidth, totalHeight);
        SetCanvasSize(GridLayer, totalWidth, totalHeight);
        SetCanvasSize(TodayLayer, totalWidth, totalHeight);
        SetCanvasSize(TaskLayer, totalWidth, totalHeight);
        SetCanvasSize(RelationLayer, totalWidth, totalHeight);
        SetCanvasSize(OverlayLayer, totalWidth, totalHeight);

        HeaderCanvas.Width = totalWidth;
        HeaderCanvas.Height = HeaderHeight;

        RenderGrid();
        RenderHeader();
        RenderTasks();
        RenderTodayLine();
        RenderRelations();
        RenderOverlay();
    }

    private (double Width, double Height) CalculateCanvasSize()
    {
        if (ProjectManager == null || ProjectManager.Tasks.Count == 0)
            return (ActualWidth, ActualHeight);

        var maxEnd = ProjectManager.Tasks.Max(t => t.End);
        var totalDays = Math.Max(maxEnd.Days + 30, 60);

        var width = totalDays * ColumnWidth;
        var height = Math.Max(ProjectManager.Tasks.Count * RowHeight + 20, ActualHeight);

        return (width, height);
    }

    private void SetCanvasSize(Canvas canvas, double width, double height)
    {
        canvas.Width = width;
        canvas.Height = height;
    }

    private void ClearAllLayers()
    {
        GridLayer.Children.Clear();
        TodayLayer.Children.Clear();
        TaskLayer.Children.Clear();
        RelationLayer.Children.Clear();
        OverlayLayer.Children.Clear();
        HeaderCanvas.Children.Clear();
    }

    private void RenderGrid()
    {
        GridLayer.Children.Clear();
        _gridRenderer.Render(GridLayer);
    }

    private void RenderHeader()
    {
        HeaderCanvas.Children.Clear();
        _headerRenderer.Render(HeaderCanvas);
    }

    private void RenderTasks()
    {
        TaskLayer.Children.Clear();
        _taskRenderer.Render(TaskLayer);
    }

    private void RenderTodayLine()
    {
        TodayLayer.Children.Clear();

        if (!ShowTodayLine || ProjectManager == null)
            return;

        var todayOffset = (DateTime.Today - ProjectManager.Start).Days;
        if (todayOffset < 0)
            return;

        var x = todayOffset * ColumnWidth + ColumnWidth / 2;

        var line = new Line
        {
            X1 = x,
            Y1 = 0,
            X2 = x,
            Y2 = TodayLayer.Height,
            Stroke = FindResource("TodayLineBrush") as Brush ?? Brushes.Red,
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 2 }
        };

        TodayLayer.Children.Add(line);
    }

    private void RenderRelations()
    {
        RelationLayer.Children.Clear();

        if (!ShowRelations || ProjectManager == null)
            return;

        // TODO: Реализовать в RelationRenderer
    }

    private void RenderOverlay()
    {
        OverlayLayer.Children.Clear();

        if (SelectedTask == null || ProjectManager == null)
            return;

        var visibleTasks = GetVisibleTasks();
        var index = visibleTasks.IndexOf(SelectedTask);
        if (index < 0)
            return;

        var x = SelectedTask.Start.Days * ColumnWidth;
        var y = index * RowHeight + BarSpacing / 2;
        var width = Math.Max(SelectedTask.Duration.Days * ColumnWidth, ColumnWidth);

        var selectionRect = new Rectangle
        {
            Width = width + 4,
            Height = BarHeight + 4,
            Stroke = FindResource("SelectionBrush") as Brush ?? Brushes.Blue,
            StrokeThickness = 2,
            Fill = Brushes.Transparent,
            RadiusX = 4,
            RadiusY = 4
        };

        Canvas.SetLeft(selectionRect, x - 2);
        Canvas.SetTop(selectionRect, y - 2);

        OverlayLayer.Children.Add(selectionRect);
    }

    #endregion

    #region Private Methods - Hit Testing

    /// <summary>
    /// Hit-test задачи (возвращает только Task).
    /// </summary>
    private Task? HitTestTask(Point position)
    {
        return HitTestTaskWithIndex(position).Task;
    }

    /// <summary>
    /// Hit-test задачи с индексом строки.
    /// </summary>
    private (Task? Task, int RowIndex) HitTestTaskWithIndex(Point position)
    {
        if (ProjectManager == null)
            return (null, -1);

        var visibleTasks = GetVisibleTasks();
    
        var rowIndex = (int)(position.Y / RowHeight);

        if (rowIndex < 0 || rowIndex >= visibleTasks.Count)
            return (null, -1);

        var task = visibleTasks[rowIndex];

        var taskX = task.Start.Days * ColumnWidth;
        var taskWidth = Math.Max(task.Duration.Days * ColumnWidth, ColumnWidth);

        // Проверяем попадание в бар задачи
        if (position.X >= taskX && position.X <= taskX + taskWidth)
        {
            return (task, rowIndex);
        }

        // Проверяем попадание в область имени задачи
        var nameAreaX = taskX + taskWidth;
        var nameAreaWidth = 200;

        if (position.X >= nameAreaX && position.X <= nameAreaX + nameAreaWidth)
        {
            return (task, rowIndex);
        }

        // Проверяем попадание в дедлайн
        if (task.Deadline.HasValue)
        {
            var deadlineX = task.Deadline.Value.Days * ColumnWidth;
            if (Math.Abs(position.X - deadlineX) <= DeadlineGrabZone)
            {
                return (task, rowIndex);
            }
        }

        return (null, -1);
    }

    /// <summary>
    /// Возвращает список видимых задач.
    /// </summary>
    private List<Task> GetVisibleTasks()
    {
        if (ProjectManager == null)
            return new List<Task>();

        var result = new List<Task>();

        foreach (var task in ProjectManager.Tasks)
        {
            if (!IsTaskHidden(task))
            {
                result.Add(task);
            }
        }

        return result;
    }

    /// <summary>
    /// Проверяет, скрыта ли задача.
    /// </summary>
    private bool IsTaskHidden(Task task)
    {
        if (ProjectManager == null)
            return false;

        foreach (var group in ProjectManager.GroupsOf(task))
        {
            if (group.IsCollapsed)
                return true;
        }

        return false;
    }

    #endregion
    
    #region Private Methods - Drag & Drop
    
    /// <summary>
    /// Добавляет или убирает дедлайн для задачи.
    /// </summary>
    private void ToggleDeadline(Task task)
    {
        if (task == null || ProjectManager == null) return;

        if (ProjectManager.IsGroup(task)) return;

        if (task.Deadline.HasValue)
        {
            ProjectManager.SetDeadline(task, null);
            StatusMessage = $"Дедлайн удалён: '{task.Name}'";
        }
        else
        {
            var newDeadline = task.End + TimeSpan.FromDays(DefaultDeadlineOffsetDays);
            ProjectManager.SetDeadline(task, newDeadline);
            StatusMessage = $"Дедлайн добавлен: '{task.Name}' ({newDeadline.Days} дн.)";
        }

        InvalidateChart();
        TaskModified?.Invoke(this, new TaskDragEventArgs 
        { 
            Task = task, 
            Operation = DragOperation.None 
        });
    }

    /// <summary>
    /// Удаляет дедлайн у задачи.
    /// </summary>
    private void RemoveDeadline(Task task)
    {
        if (task == null || ProjectManager == null) return;

        if (task.Deadline.HasValue)
        {
            ProjectManager.SetDeadline(task, null);
            StatusMessage = $"Дедлайн удалён: '{task.Name}'";
            InvalidateChart();
            TaskModified?.Invoke(this, new TaskDragEventArgs 
            { 
                Task = task, 
                Operation = DragOperation.None 
            });
        }
    }

    /// <summary>
    /// Проверяет, находится ли курсор над дедлайном задачи.
    /// </summary>
    private bool IsDeadlineSelected(Task task)
    {
        if (task == null || !task.Deadline.HasValue) return false;

        var position = Mouse.GetPosition(TaskLayer);
        var deadlineX = task.Deadline.Value.Days * ColumnWidth;

        return Math.Abs(position.X - deadlineX) <= DeadlineGrabZone;
    }
    
    /// <summary>
    /// Проверяет, можно ли изменять прогресс задачи.
    /// </summary>
    private bool CanAdjustProgress(Task task)
    {
        if (ProjectManager == null) return false;

        if (ProjectManager.IsGroup(task)) return false;
        if (ProjectManager.IsSplit(task)) return false;

        return true;
    }
    
    /// <summary>
    /// Начинает операцию изменения прогресса.
    /// </summary>
    private void StartProgressAdjust(Task task, Point startPoint)
    {
        _dragState = new DragState
        {
            Task = task,
            Operation = DragOperation.ProgressAdjusting,
            StartPoint = startPoint,
            OriginalStart = task.Start,
            OriginalDuration = task.Duration,
            OriginalEnd = task.End,
            OriginalComplete = task.Complete,
            OriginalDeadline = task.Deadline,
            OriginalIndex = GetRealTaskIndex(task)
        };

        CaptureMouse();
        Cursor = Cursors.SizeWE;

        RenderProgressPreview();
    }
    
    /// <summary>
    /// Обновляет preview при изменении прогресса.
    /// </summary>
    private void UpdateProgressPreview(Point currentPoint)
    {
        if (_dragState == null || _dragState.Task == null || ProjectManager == null)
            return;

        var deltaX = currentPoint.X - _dragState.StartPoint.X;
        
        var deltaPercent = (float)(deltaX / PixelsPerProgressPercent / 100.0);
        var newComplete = _dragState.OriginalComplete + deltaPercent;

        newComplete = Math.Clamp(newComplete, 0f, 1f);

        ProjectManager.SetComplete(_dragState.Task, newComplete);

        RenderProgressPreview();
    }
    
    /// <summary>
    /// Рисует preview изменения прогресса.
    /// </summary>
    private void RenderProgressPreview()
    {
        OverlayLayer.Children.Clear();

        if (_dragState == null || _dragState.Task == null || ProjectManager == null)
            return;

        var task = _dragState.Task;
        var visibleTasks = GetVisibleTasks();
        var index = visibleTasks.IndexOf(task);
        if (index < 0) return;

        var x = task.Start.Days * ColumnWidth;
        var y = index * RowHeight + BarSpacing / 2;
        var width = Math.Max(task.Duration.Days * ColumnWidth, ColumnWidth);

        var selectionRect = new Rectangle
        {
            Width = width + 4,
            Height = BarHeight + 4,
            Stroke = Brushes.Orange,
            StrokeThickness = 2,
            Fill = Brushes.Transparent,
            RadiusX = 4,
            RadiusY = 4
        };

        Canvas.SetLeft(selectionRect, x - 2);
        Canvas.SetTop(selectionRect, y - 2);
        OverlayLayer.Children.Add(selectionRect);

        var progressWidth = width * task.Complete;
        var progressRect = new Rectangle
        {
            Width = Math.Max(progressWidth, 0),
            Height = BarHeight,
            Fill = new SolidColorBrush(Color.FromArgb(180, 255, 165, 0)),
            RadiusX = 3,
            RadiusY = 3
        };

        Canvas.SetLeft(progressRect, x);
        Canvas.SetTop(progressRect, y);
        OverlayLayer.Children.Add(progressRect);

        var percent = (int)(task.Complete * 100);
        var tooltipBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(50, 50, 50)),
            BorderBrush = Brushes.Orange,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 2, 6, 2),
            Child = new TextBlock
            {
                Text = $"{percent}%",
                Foreground = Brushes.White,
                FontSize = 12,
                FontWeight = FontWeights.Bold
            }
        };

        tooltipBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        
        var tooltipX = x + (width - tooltipBorder.DesiredSize.Width) / 2;
        var tooltipY = y - tooltipBorder.DesiredSize.Height - 4;

        if (tooltipY < 0)
            tooltipY = y + BarHeight + 4;

        Canvas.SetLeft(tooltipBorder, Math.Max(0, tooltipX));
        Canvas.SetTop(tooltipBorder, Math.Max(0, tooltipY));
        OverlayLayer.Children.Add(tooltipBorder);

        var arrowY = y + BarHeight / 2;
        var arrowStartX = x + progressWidth;
        
        var arrowLine = new Line
        {
            X1 = arrowStartX,
            Y1 = arrowY,
            X2 = arrowStartX + (task.Complete < _dragState.OriginalComplete ? -15 : 15),
            Y2 = arrowY,
            Stroke = Brushes.Orange,
            StrokeThickness = 2
        };
        OverlayLayer.Children.Add(arrowLine);
    }
        
    /// <summary>
    /// Завершает операцию изменения прогресса.
    /// </summary>
    private void CompleteProgressAdjust(Point endPoint)
    {
        if (_dragState == null || _dragState.Task == null || ProjectManager == null)
        {
            CancelDrag();
            return;
        }

        var task = _dragState.Task;

        var args = new TaskDragEventArgs(
            task,
            DragOperation.ProgressAdjusting,
            _dragState.OriginalStart,
            task.Start,
            _dragState.OriginalDuration,
            task.Duration,
            _dragState.OriginalIndex,
            GetRealTaskIndex(task),
            _dragState.OriginalComplete,
            task.Complete);

        FinalizeDrag();

        TaskDragged?.Invoke(this, args);
    }


    /// <summary>
    /// Проверяет, можно ли перетаскивать задачу.
    /// </summary>
    private bool CanDragTask(Task task)
    {
        if (ProjectManager == null) return false;

        if (ProjectManager.IsPart(task)) return false;

        return true;
    }

    /// <summary>
    /// Определяет тип операции по позиции клика.
    /// </summary>
    private DragOperation DetermineOperation(Task task, Point clickPosition, int rowIndex)
    {
        var columnWidth = ColumnWidth;
        var barHeight = BarHeight;
        var barSpacing = BarSpacing;
        var rowHeight = RowHeight;

        var taskX = task.Start.Days * columnWidth;
        var taskY = rowIndex * rowHeight + barSpacing / 2;
        var taskWidth = Math.Max(task.Duration.Days * columnWidth, columnWidth * 0.5);
        var taskEndX = taskX + taskWidth;

        // Проверка дедлайна
        if (task.Deadline.HasValue)
        {
            var deadlineX = task.Deadline.Value.Days * columnWidth;
            
            if (Math.Abs(clickPosition.X - deadlineX) <= DeadlineGrabZone &&
                clickPosition.Y >= taskY - 4 &&
                clickPosition.Y <= taskY + barHeight + 4)
            {
                return DragOperation.DeadlineMoving;
            }
        }

        // Группы — только перемещение
        var isGroup = ProjectManager?.IsGroup(task) ?? false;
        if (isGroup)
        {
            if (clickPosition.X >= taskX && clickPosition.X <= taskEndX &&
                clickPosition.Y >= taskY && clickPosition.Y <= taskY + barHeight)
            {
                return DragOperation.Moving;
            }
            return DragOperation.None;
        }

        // Split-части — нельзя тянуть
        // var isSplitPart = task.IsPart;
        // if (isSplitPart)
        // {
        //     return DragOperation.None;
        // }

        // Обычные задачи
        const double resizeZone = 8.0;

        if (clickPosition.Y < taskY || clickPosition.Y > taskY + barHeight)
        {
            return DragOperation.None;
        }

        // Левый край
        if (clickPosition.X >= taskX - resizeZone && clickPosition.X <= taskX + resizeZone)
        {
            return DragOperation.ResizingStart;
        }

        // Правый край
        if (clickPosition.X >= taskEndX - resizeZone && clickPosition.X <= taskEndX + resizeZone)
        {
            return DragOperation.ResizingEnd;
        }

        // Центр
        if (clickPosition.X > taskX + resizeZone && clickPosition.X < taskEndX - resizeZone)
        {
            return DragOperation.Moving;
        }

        return DragOperation.None;
    }

    /// <summary>
    /// Обновляет курсор при наведении на задачу.
    /// </summary>
    private void UpdateCursor(Point position)
    {
        if (_dragState != null && _dragState.IsActive)
            return;

        var hitResult = HitTestTaskWithIndex(position);

        if (hitResult.Task == null)
        {
            Cursor = Cursors.Arrow;
            return;
        }

        var task = hitResult.Task;
        var rowIndex = hitResult.RowIndex;

        // Проверяем дедлайн
        if (task.Deadline.HasValue)
        {
            var deadlineX = task.Deadline.Value.Days * ColumnWidth;
            var taskY = rowIndex * RowHeight + BarSpacing / 2;
            
            if (Math.Abs(position.X - deadlineX) <= DeadlineGrabZone &&
                position.Y >= taskY - 4 &&
                position.Y <= taskY + BarHeight + 4)
            {
                Cursor = Cursors.SizeWE;
                return;
            }
        }

        var isGroup = ProjectManager?.IsGroup(task) ?? false;
        // var isSplitPart = task.IsPart;
        //
        // if (isSplitPart)
        // {
        //     Cursor = Cursors.No;
        //     return;
        // }

        var columnWidth = ColumnWidth;
        var taskX = task.Start.Days * columnWidth;
        var taskWidth = Math.Max(task.Duration.Days * columnWidth, columnWidth * 0.5);
        var taskEndX = taskX + taskWidth;

        const double resizeZone = 8.0;

        if (isGroup)
        {
            if (position.X >= taskX && position.X <= taskEndX)
            {
                Cursor = Cursors.SizeAll;
            }
            else
            {
                Cursor = Cursors.Arrow;
            }
            return;
        }

        // Левый край
        if (position.X >= taskX - resizeZone && position.X <= taskX + resizeZone)
        {
            Cursor = Cursors.SizeWE;
        }
        // Правый край
        else if (position.X >= taskEndX - resizeZone && position.X <= taskEndX + resizeZone)
        {
            Cursor = Cursors.SizeWE;
        }
        // Центр
        else if (position.X > taskX + resizeZone && position.X < taskEndX - resizeZone)
        {
            Cursor = Cursors.SizeAll;
        }
        else
        {
            Cursor = Cursors.Arrow;
        }
    }


    /// <summary>
    /// Начинает операцию перетаскивания.
    /// </summary>
    private void StartDrag(Task task, Point position, int rowIndex, DragOperation operation)
    {
        _dragState = new DragState
        {
            Task = task,
            Operation = operation,
            StartPoint = position,
            OriginalStart = task.Start,
            OriginalDuration = task.Duration,
            OriginalEnd = task.End,
            OriginalComplete = task.Complete,
            OriginalDeadline = task.Deadline,
            OriginalIndex = rowIndex
        };

        Mouse.Capture(this);

        switch (operation)
        {
            case DragOperation.Moving:
                Cursor = Cursors.SizeAll;
                break;
            case DragOperation.ResizingStart:
            case DragOperation.ResizingEnd:
            case DragOperation.DeadlineMoving:
                Cursor = Cursors.SizeWE;
                break;
            case DragOperation.ProgressAdjusting:
                Cursor = Cursors.SizeWE;
                break;
        }

        if (operation == DragOperation.Moving && ProjectManager != null && ProjectManager.IsGroup(task))
        {
            _originalChildPositions = new Dictionary<Guid, (TimeSpan Start, TimeSpan Duration)>();
            foreach (var member in ProjectManager.MembersOf(task))
            {
                _originalChildPositions[member.Id] = (member.Start, member.Duration);
            }
        }
    }

    /// <summary>
    /// Обновляет preview во время перетаскивания.
    /// </summary>
    private void UpdateDragPreview(Point currentPoint)
    {
        if (_dragState == null || _dragState.Task == null || ProjectManager == null)
            return;

        var deltaX = currentPoint.X - _dragState.StartPoint.X;
        var deltaY = currentPoint.Y - _dragState.StartPoint.Y;

        switch (_dragState.Operation)
        {
            case DragOperation.Moving:
                UpdateMovingPreview(deltaX);
                break;

            case DragOperation.ResizingStart:
                UpdateResizingStartPreview(deltaX);
                break;

            case DragOperation.ResizingEnd:
                UpdateResizingEndPreview(deltaX);
                break;

            case DragOperation.Reordering:
                UpdateReorderingPreview(deltaY);
                break;
        }

        RenderDragPreview();
    }

    private void UpdateMovingPreview(double deltaX)
    {
        if (_dragState == null || _dragState.Task == null || ProjectManager == null)
            return;

        var deltaDays = (int)Math.Round(deltaX / ColumnWidth);
        var newStart = _dragState.OriginalStart + TimeSpan.FromDays(deltaDays);

        if (newStart < TimeSpan.Zero)
            newStart = TimeSpan.Zero;

        ProjectManager.SetStart(_dragState.Task, newStart);
    }

    private void UpdateResizingStartPreview(double deltaX)
    {
        if (_dragState == null || _dragState.Task == null || ProjectManager == null)
            return;

        var deltaDays = (int)Math.Round(deltaX / ColumnWidth);
        var newStart = _dragState.OriginalStart + TimeSpan.FromDays(deltaDays);
        var originalEnd = _dragState.OriginalEnd;

        if (newStart < TimeSpan.Zero)
            newStart = TimeSpan.Zero;

        if (newStart >= originalEnd - TimeSpan.FromDays(MinDurationDays))
            newStart = originalEnd - TimeSpan.FromDays(MinDurationDays);

        ProjectManager.SetStart(_dragState.Task, newStart);
        ProjectManager.SetEnd(_dragState.Task, originalEnd);
    }

    private void UpdateResizingEndPreview(double deltaX)
    {
        if (_dragState == null || _dragState.Task == null || ProjectManager == null)
            return;

        var deltaDays = (int)Math.Round(deltaX / ColumnWidth);
        var newEnd = _dragState.OriginalEnd + TimeSpan.FromDays(deltaDays);

        if (newEnd <= _dragState.Task.Start + TimeSpan.FromDays(MinDurationDays))
            newEnd = _dragState.Task.Start + TimeSpan.FromDays(MinDurationDays);

        ProjectManager.SetEnd(_dragState.Task, newEnd);
    }

    private void UpdateReorderingPreview(double deltaY)
    {
        // Визуальная индикация через RenderReorderIndicator
    }

    private void RenderDragPreview()
    {
        OverlayLayer.Children.Clear();

        if (_dragState == null || _dragState.Task == null || ProjectManager == null)
            return;

        var task = _dragState.Task;
        var visibleTasks = GetVisibleTasks();

        // Проверяем, является ли задача группой
        var isGroup = ProjectManager.IsGroup(task);

        if (isGroup)
        {
            RenderGroupDragPreview(task, visibleTasks);
        }
        else
        {
            RenderTaskDragPreview(task, visibleTasks, true);
        }

        if (_dragState.Operation == DragOperation.Reordering)
        {
            RenderReorderIndicator();
        }
    }

    /// <summary>
    /// Рисует preview группы со всеми дочерними задачами.
    /// </summary>
    private void RenderGroupDragPreview(Task groupTask, List<Task> visibleTasks)
    {
        // Сначала рисуем саму группу
        RenderTaskDragPreview(groupTask, visibleTasks, true);

        // Затем все дочерние задачи
        if (ProjectManager == null) return;
        
        foreach (var member in ProjectManager.MembersOf(groupTask))
        {
            RenderTaskDragPreview(member, visibleTasks, false);
        }
    }

    /// <summary>
    /// Рисует preview одной задачи.
    /// </summary>
    private void RenderTaskDragPreview(Task task, List<Task> visibleTasks, bool isMainTask)
    {
        var index = visibleTasks.IndexOf(task);
        if (index < 0) return;

        var x = task.Start.Days * ColumnWidth;
        var y = index * RowHeight + BarSpacing / 2;
        var width = Math.Max(task.Duration.Days * ColumnWidth, ColumnWidth);

        byte alpha = isMainTask ? (byte)160 : (byte)100;
        var fillColor = isMainTask 
            ? Color.FromArgb(alpha, 70, 130, 180)
            : Color.FromArgb(alpha, 100, 149, 237);

        var previewRect = new Rectangle
        {
            Width = width,
            Height = BarHeight,
            Fill = new SolidColorBrush(fillColor),
            Stroke = isMainTask 
                ? (FindResource("SelectionBrush") as Brush ?? Brushes.Blue)
                : new SolidColorBrush(Color.FromArgb(180, 70, 130, 180)),
            StrokeThickness = isMainTask ? 2 : 1,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            RadiusX = 3,
            RadiusY = 3
        };

        Canvas.SetLeft(previewRect, x);
        Canvas.SetTop(previewRect, y);
        OverlayLayer.Children.Add(previewRect);

        if (isMainTask)
        {
            var selectionRect = new Rectangle
            {
                Width = width + 4,
                Height = BarHeight + 4,
                Stroke = FindResource("SelectionBrush") as Brush ?? Brushes.Blue,
                StrokeThickness = 2,
                Fill = Brushes.Transparent,
                RadiusX = 4,
                RadiusY = 4
            };

            Canvas.SetLeft(selectionRect, x - 2);
            Canvas.SetTop(selectionRect, y - 2);
            OverlayLayer.Children.Add(selectionRect);
        }
    }

    /// <summary>
    /// Рисует индикатор позиции для переупорядочивания.
    /// </summary>
    private void RenderReorderIndicator()
    {
        var mousePos = Mouse.GetPosition(TaskLayer);
        var visibleTasks = GetVisibleTasks();
        var targetIndex = (int)(mousePos.Y / RowHeight);
        targetIndex = Math.Max(0, Math.Min(targetIndex, visibleTasks.Count - 1));

        var indicatorY = targetIndex * RowHeight;

        var line = new Line
        {
            X1 = 0,
            Y1 = indicatorY,
            X2 = TaskLayer.ActualWidth,
            Y2 = indicatorY,
            Stroke = FindResource("SelectionBrush") as Brush ?? Brushes.Blue,
            StrokeThickness = 3
        };

        OverlayLayer.Children.Add(line);

        var arrow = new Polygon
        {
            Points = new PointCollection
            {
                new Point(0, indicatorY - 6),
                new Point(10, indicatorY),
                new Point(0, indicatorY + 6)
            },
            Fill = FindResource("SelectionBrush") as Brush ?? Brushes.Blue
        };

        OverlayLayer.Children.Add(arrow);
    }

    /// <summary>
    /// Завершает операцию перетаскивания.
    /// </summary>
    private void CompleteDrag(Point endPoint)
    {
        if (_dragState == null || _dragState.Task == null || ProjectManager == null)
        {
            CancelDrag();
            return;
        }

        var task = _dragState.Task;
        var operation = _dragState.Operation;

        switch (operation)
        {
            case DragOperation.Reordering:
            {
                var visibleTasks = GetVisibleTasks();
                if (visibleTasks.Count > 0)
                {
                    var targetIndex = (int)(endPoint.Y / RowHeight);
                    targetIndex = Math.Max(0, Math.Min(targetIndex, visibleTasks.Count - 1));

                    if (targetIndex != _dragState.OriginalIndex)
                    {
                        MoveTaskToIndex(task, _dragState.OriginalIndex, targetIndex);
                    }
                }
                break;
            }
            
            case DragOperation.DeadlineMoving:
                StatusMessage = $"Дедлайн '{_dragState.Task.Name}': {_dragState.Task.Deadline?.Days} дн.";
                break;

            default:
                break;
        }

        var args = new TaskDragEventArgs(
            task,
            operation,
            _dragState.OriginalStart,
            task.Start,
            _dragState.OriginalDuration,
            task.Duration,
            _dragState.OriginalIndex,
            GetRealTaskIndex(task),
            _dragState.OriginalComplete,
            task.Complete);

        FinalizeDrag();

        TaskDragged?.Invoke(this, args);
    }

    /// <summary>
    /// Перемещает задачу на указанный индекс.
    /// </summary>
    private void MoveTaskToIndex(Task task, int fromIndex, int toIndex)
    {
        if (ProjectManager == null || fromIndex == toIndex) return;

        var offset = toIndex - fromIndex;
        ProjectManager.Move(task, offset);
    }

    /// <summary>
    /// Отменяет операцию перетаскивания.
    /// </summary>
    private void CancelDrag()
    {
        if (_dragState == null) return;

        var task = _dragState.Task;

        if (task != null)
        {
            switch (_dragState.Operation)
            {
                case DragOperation.Moving:
                case DragOperation.ResizingStart:
                case DragOperation.ResizingEnd:
                    ProjectManager?.SetStart(task, _dragState.OriginalStart);
                    ProjectManager?.SetEnd(task, _dragState.OriginalEnd);
                    
                    if (_originalChildPositions != null && ProjectManager != null)
                    {
                        foreach (var member in ProjectManager.MembersOf(task))
                        {
                            if (_originalChildPositions.TryGetValue(member.Id, out var original))
                            {
                                ProjectManager.SetStart(member, original.Start);
                                ProjectManager.SetDuration(member, original.Duration);
                            }
                        }
                    }
                    break;

                case DragOperation.ProgressAdjusting:
                    ProjectManager?.SetComplete(task, _dragState.OriginalComplete);
                    break;

                case DragOperation.DeadlineMoving:
                    ProjectManager?.SetDeadline(task, _dragState.OriginalDeadline);
                    break;
            }
        }

        _dragState = null;
        _originalChildPositions = null;

        Mouse.Capture(null);
        Cursor = Cursors.Arrow;

        InvalidateChart();
    }

    /// <summary>
    /// Финализирует операцию перетаскивания.
    /// </summary>
    private void FinalizeDrag()
    {
        _dragState = null;
        _originalChildPositions = null;
        ReleaseMouseCapture();
        Cursor = Cursors.Arrow;
        OverlayLayer.Children.Clear();
        RenderOverlay();
        Render();
    }

    #endregion
}

/// <summary>
/// Состояние операции перетаскивания.
/// </summary>
public class DragState
{
    public Task? Task { get; set; }
    public DragOperation Operation { get; set; }
    public Point StartPoint { get; set; }
    public TimeSpan OriginalStart { get; set; }
    public TimeSpan OriginalDuration { get; set; }
    public TimeSpan OriginalEnd { get; set; }
    public float OriginalComplete { get; set; }
    public TimeSpan? OriginalDeadline { get; set; }
    public int OriginalIndex { get; set; }
    
    public bool IsActive => Task != null;
    
    public void Reset()
    {
        Task = null;
        Operation = DragOperation.None;
        StartPoint = default;
        OriginalStart = default;
        OriginalDuration = default;
        OriginalEnd = default;
        OriginalComplete = 0;
        OriginalDeadline = null;
        OriginalIndex = -1;
    }
}

/// <summary>
/// Тип операции перетаскивания.
/// </summary>
public enum DragOperation
{
    None,
    Moving,
    ResizingStart,
    ResizingEnd,
    Reordering,
    ProgressAdjusting,
    DeadlineMoving
}

/// <summary>
/// Аргументы события перетаскивания задачи.
/// </summary>
public class TaskDragEventArgs : EventArgs
{
    public Task Task { get; set; } = null!;
    public DragOperation Operation { get; set; }
    public TimeSpan OldStart { get; set; }
    public TimeSpan NewStart { get; set; }
    public TimeSpan OldDuration { get; set; }
    public TimeSpan NewDuration { get; set; }
    public int OldIndex { get; set; }
    public int NewIndex { get; set; }
    public float OldComplete { get; set; }
    public float NewComplete { get; set; }

    public TaskDragEventArgs() { }

    public TaskDragEventArgs(
        Task task,
        DragOperation operation,
        TimeSpan oldStart,
        TimeSpan newStart,
        TimeSpan oldDuration,
        TimeSpan newDuration,
        int oldIndex,
        int newIndex,
        float oldComplete,
        float newComplete)
    {
        Task = task;
        Operation = operation;
        OldStart = oldStart;
        NewStart = newStart;
        OldDuration = oldDuration;
        NewDuration = newDuration;
        OldIndex = oldIndex;
        NewIndex = newIndex;
        OldComplete = oldComplete;
        NewComplete = newComplete;
    }
}