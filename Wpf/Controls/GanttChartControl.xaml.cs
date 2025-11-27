// GanttChart.WPF/Controls/GanttChartControl.xaml.cs
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
    
    // Drag & Drop
    private readonly DragState _dragState = new();
    private const double EdgeZoneWidth = 8.0;  // Ширина зоны для resize
    private const double MinDurationDays = 1.0;

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

    #endregion

    #region Constructor

    public GanttChartControl()
    {
        InitializeComponent();

        // Инициализация рендереров
        _gridRenderer = new GridRenderer(this);
        _headerRenderer = new HeaderRenderer(this);
        _taskRenderer = new TaskRenderer(this);

        // Подписка на события
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    
        // ВАЖНО: Используем Preview события, чтобы перехватить до ScrollViewer
        PreviewMouseWheel += OnPreviewMouseWheel;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseMove += OnMouseMove;
        PreviewKeyDown += OnPreviewKeyDown;
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
            // Пересчитываем ширину колонки на основе zoom
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
        // Синхронизация горизонтального скролла заголовка с основным контентом
        HeaderScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);
    }
    
    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Ctrl + Wheel = Zoom
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            var delta = e.Delta > 0 ? 10 : -10;
            var newZoom = Math.Clamp(ZoomLevel + delta, 50, 200);
            ZoomLevel = newZoom;
            e.Handled = true; // Предотвращаем скролл
        }
        // Без Ctrl - позволяем ScrollViewer обрабатывать скролл
    }
    
    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(TaskLayer);
        var task = HitTestTask(position);

        if (task != null)
        {
            SelectedTask = task;

            // Обработка двойного клика
            if (e.ClickCount == 2)
            {
                TaskDoubleClicked?.Invoke(this, task);
                return;
            }

            // Проверяем, можно ли перетаскивать эту задачу
            if (!CanDragTask(task))
                return;

            // Определяем зону и тип операции
            var zone = GetHitZone(task, position);
            var operation = DetermineOperation(zone, Keyboard.Modifiers);

            if (operation != DragOperation.None)
            {
                StartDrag(task, position, operation);
                e.Handled = true;
            }
        }
        else
        {
            SelectedTask = null;
        }
    }
    
    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragState.IsActive) return;
        var position = e.GetPosition(TaskLayer);
        CompleteDrag(position);
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(TaskLayer);

        if (_dragState.IsActive)
        {
            // Обновляем preview во время перетаскивания
            UpdateDragPreview(position);
        }
        else
        {
            // Обновляем курсор при наведении
            UpdateCursor(position);
        }
    }
    
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || !_dragState.IsActive) return;
        CancelDrag();
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
    /// Вызывается извне при изменении данных.
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
    /// Используется после структурных изменений (удаление/добавление задач).
    /// </summary>
    public void ForceFullRedraw()
    {
        // Очищаем все слои
        ClearAllLayers();
    
        // Сбрасываем флаг рендеринга
        _isRendering = false;
    
        // Принудительно вызываем рендеринг
        if (IsLoaded && ProjectManager != null)
        {
            Render();
        }
    }

    #endregion

    #region Private Methods - Rendering

    /// <summary>
    /// Возвращает реальный индекс задачи в списке Tasks.
    /// Обходит проблему с кэшированными индексами в ProjectManager.IndexOf().
    /// </summary>
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

        // Вычисляем размеры
        var (totalWidth, totalHeight) = CalculateCanvasSize();

        // Устанавливаем размеры Canvas
        SetCanvasSize(ChartCanvas, totalWidth, totalHeight);
        SetCanvasSize(GridLayer, totalWidth, totalHeight);
        SetCanvasSize(TodayLayer, totalWidth, totalHeight);
        SetCanvasSize(TaskLayer, totalWidth, totalHeight);
        SetCanvasSize(RelationLayer, totalWidth, totalHeight);
        SetCanvasSize(OverlayLayer, totalWidth, totalHeight);

        HeaderCanvas.Width = totalWidth;
        HeaderCanvas.Height = HeaderHeight;

        // Рендерим слои
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

        // Находим максимальную дату
        var maxEnd = ProjectManager.Tasks.Max(t => t.End);
        var totalDays = Math.Max(maxEnd.Days + 30, 60); // Минимум 60 дней для отображения

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

        // TODO: Реализовать в RelationRenderer (Фаза 3.9)
    }

    private void RenderOverlay()
    {
        OverlayLayer.Children.Clear();

        if (SelectedTask == null || ProjectManager == null)
            return;

        var index = GetRealTaskIndex(SelectedTask);
        if (index < 0)
            return;

        // Рисуем рамку выделения
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

    private Task? HitTestTask(Point position)
    {
        if (ProjectManager == null)
            return null;

        // Получаем список ВИДИМЫХ задач (не скрытых в свёрнутых группах)
        var visibleTasks = GetVisibleTasks();
    
        var rowIndex = (int)(position.Y / RowHeight);

        if (rowIndex < 0 || rowIndex >= visibleTasks.Count)
            return null;

        var task = visibleTasks[rowIndex];

        // Проверяем, попали ли мы в бар задачи по X
        var taskX = task.Start.Days * ColumnWidth;
        var taskWidth = Math.Max(task.Duration.Days * ColumnWidth, ColumnWidth);

        if (position.X >= taskX && position.X <= taskX + taskWidth)
        {
            return task;
        }

        // Также проверяем клик по имени задачи (справа от бара)
        var nameAreaX = taskX + taskWidth;
        var nameAreaWidth = 200; // примерная ширина области имени

        if (position.X >= nameAreaX && position.X <= nameAreaX + nameAreaWidth)
        {
            return task;
        }

        return null;
    }

    /// <summary>
    /// Возвращает список видимых задач (исключая скрытые в свёрнутых группах).
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
    /// Проверяет, скрыта ли задача (находится в свёрнутой группе).
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
/// Проверяет, можно ли перетаскивать задачу.
/// </summary>
private bool CanDragTask(Task task)
{
    if (ProjectManager == null) return false;

    // Не перетаскиваем группы
    if (ProjectManager.IsGroup(task)) return false;

    // Не перетаскиваем split-части
    if (ProjectManager.IsPart(task)) return false;

    return true;
}

/// <summary>
/// Определяет зону попадания на баре задачи.
/// </summary>
private HitZone GetHitZone(Task task, Point position)
{
    var taskX = task.Start.Days * ColumnWidth;
    var taskWidth = Math.Max(task.Duration.Days * ColumnWidth, ColumnWidth);

    var relativeX = position.X - taskX;

    if (relativeX < EdgeZoneWidth)
        return HitZone.LeftEdge;

    if (relativeX > taskWidth - EdgeZoneWidth)
        return HitZone.RightEdge;

    return HitZone.Center;
}

/// <summary>
/// Определяет тип операции по зоне и модификаторам.
/// </summary>
private DragOperation DetermineOperation(HitZone zone, ModifierKeys modifiers)
{
    // Shift + любая зона = Reordering
    if (modifiers.HasFlag(ModifierKeys.Shift))
        return DragOperation.Reordering;

    return zone switch
    {
        HitZone.LeftEdge => DragOperation.ResizingStart,
        HitZone.RightEdge => DragOperation.ResizingEnd,
        HitZone.Center => DragOperation.Moving,
        _ => DragOperation.None
    };
}

/// <summary>
/// Обновляет курсор при наведении на задачу.
/// </summary>
private void UpdateCursor(Point position)
{
    var task = HitTestTask(position);

    if (task != null && CanDragTask(task))
    {
        var zone = GetHitZone(task, position);

        Cursor = zone switch
        {
            HitZone.LeftEdge => Cursors.SizeWE,
            HitZone.RightEdge => Cursors.SizeWE,
            HitZone.Center => Cursors.SizeAll,
            _ => Cursors.Arrow
        };
    }
    else
    {
        Cursor = Cursors.Arrow;
    }
}

/// <summary>
/// Начинает операцию перетаскивания.
/// </summary>
private void StartDrag(Task task, Point startPoint, DragOperation operation)
{
    _dragState.Task = task;
    _dragState.Operation = operation;
    _dragState.StartPoint = startPoint;
    _dragState.OriginalStart = task.Start;
    _dragState.OriginalDuration = task.Duration;
    _dragState.OriginalIndex = GetRealTaskIndex(task);

    // Захватываем мышь на уровне контрола, не TaskLayer
    CaptureMouse();

    // Устанавливаем курсор
    Cursor = operation switch
    {
        DragOperation.Reordering => Cursors.SizeNS,
        DragOperation.ResizingStart or DragOperation.ResizingEnd => Cursors.SizeWE,
        _ => Cursors.SizeAll
    };

    // Рисуем начальный preview
    RenderDragPreview();
}

/// <summary>
/// Обновляет preview во время перетаскивания.
/// </summary>
private void UpdateDragPreview(Point currentPoint)
{
    if (!_dragState.IsActive || _dragState.Task == null || ProjectManager == null)
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

/// <summary>
/// Обновляет preview для перемещения.
/// </summary>
private void UpdateMovingPreview(double deltaX)
{
    if (_dragState.Task == null) return;

    // Конвертируем дельту в дни
    var deltaDays = (int)Math.Round(deltaX / ColumnWidth);
    var newStart = _dragState.OriginalStart + TimeSpan.FromDays(deltaDays);

    // Не позволяем уйти в отрицательные дни
    if (newStart < TimeSpan.Zero)
        newStart = TimeSpan.Zero;

    // Напрямую устанавливаем Start, обходя логику зависимостей
    _dragState.Task.Start = newStart;
}

/// <summary>
/// Обновляет preview для изменения начала (левый край).
/// </summary>
private void UpdateResizingStartPreview(double deltaX)
{
    if (_dragState.Task == null) return;

    var deltaDays = (int)Math.Round(deltaX / ColumnWidth);
    var newStart = _dragState.OriginalStart + TimeSpan.FromDays(deltaDays);

    // Вычисляем новую длительность (End остаётся на месте)
    var originalEnd = _dragState.OriginalStart + _dragState.OriginalDuration;
    var newDuration = originalEnd - newStart;

    // Ограничения
    if (newStart < TimeSpan.Zero)
    {
        newStart = TimeSpan.Zero;
        newDuration = originalEnd;
    }

    if (newDuration < TimeSpan.FromDays(MinDurationDays))
    {
        newDuration = TimeSpan.FromDays(MinDurationDays);
        newStart = originalEnd - newDuration;
    }

    // Напрямую устанавливаем значения
    _dragState.Task.Start = newStart;
    _dragState.Task.Duration = newDuration;
}

/// <summary>
/// Обновляет preview для изменения длительности (правый край).
/// </summary>
private void UpdateResizingEndPreview(double deltaX)
{
    if (_dragState.Task == null) return;

    var deltaDays = (int)Math.Round(deltaX / ColumnWidth);
    var newDuration = _dragState.OriginalDuration + TimeSpan.FromDays(deltaDays);

    // Минимальная длительность
    if (newDuration < TimeSpan.FromDays(MinDurationDays))
        newDuration = TimeSpan.FromDays(MinDurationDays);

    // Напрямую устанавливаем Duration
    _dragState.Task.Duration = newDuration;
}

/// <summary>
/// Обновляет preview для переупорядочивания.
/// </summary>
private void UpdateReorderingPreview(double deltaY)
{
    // Пока только визуальный preview, перемещение при завершении
    // Линия-индикатор рисуется в RenderDragPreview
}

/// <summary>
/// Рисует preview перетаскивания.
/// </summary>
private void RenderDragPreview()
{
    OverlayLayer.Children.Clear();

    if (!_dragState.IsActive || _dragState.Task == null || ProjectManager == null)
        return;

    var task = _dragState.Task;
    var index = GetRealTaskIndex(task);
    if (index < 0) return;

    // Координаты бара
    var x = task.Start.Days * ColumnWidth;
    var y = index * RowHeight + BarSpacing / 2;
    var width = Math.Max(task.Duration.Days * ColumnWidth, ColumnWidth);

    if (_dragState.Operation == DragOperation.Reordering)
    {
        // Рисуем линию-индикатор позиции
        RenderReorderIndicator();
    }

    // Полупрозрачный preview бара
    var previewRect = new Rectangle
    {
        Width = width,
        Height = BarHeight,
        Fill = new SolidColorBrush(Color.FromArgb(128, 70, 130, 180)), // Полупрозрачный
        Stroke = FindResource("SelectionBrush") as Brush ?? Brushes.Blue,
        StrokeThickness = 2,
        StrokeDashArray = new DoubleCollection { 4, 2 },
        RadiusX = 3,
        RadiusY = 3
    };

    Canvas.SetLeft(previewRect, x);
    Canvas.SetTop(previewRect, y);

    OverlayLayer.Children.Add(previewRect);

    // Рамка выделения поверх
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

/// <summary>
/// Рисует индикатор позиции для переупорядочивания.
/// </summary>
private void RenderReorderIndicator()
{
    // Вычисляем целевую позицию на основе текущей позиции мыши
    var mousePos = Mouse.GetPosition(TaskLayer);
    var targetIndex = (int)(mousePos.Y / RowHeight);
    targetIndex = Math.Max(0, Math.Min(targetIndex, (ProjectManager?.Tasks.Count ?? 1) - 1));

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

    // Треугольный указатель слева
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
    if (!_dragState.IsActive || _dragState.Task == null || ProjectManager == null)
    {
        CancelDrag();
        return;
    }

    var task = _dragState.Task;
    var operation = _dragState.Operation;

    // Для Reordering — выполняем перемещение
    if (operation == DragOperation.Reordering)
    {
        var targetIndex = (int)(endPoint.Y / RowHeight);
        targetIndex = Math.Max(0, Math.Min(targetIndex, ProjectManager.Tasks.Count - 1));

        if (targetIndex != _dragState.OriginalIndex)
        {
            MoveTaskToIndex(task, _dragState.OriginalIndex, targetIndex);
        }
    }

    // Создаём событие для Undo/Redo
    var args = new TaskDragEventArgs(
        task,
        operation,
        _dragState.OriginalStart,
        task.Start,
        _dragState.OriginalDuration,
        task.Duration,
        _dragState.OriginalIndex,
        GetRealTaskIndex(task));

    // Очищаем состояние
    FinalizeDrag();

    // Вызываем событие
    TaskDragged?.Invoke(this, args);
}


/// <summary>
/// Перемещает задачу на указанный индекс.
/// </summary>
private void MoveTaskToIndex(Task task, int fromIndex, int toIndex)
{
    if (ProjectManager == null || fromIndex == toIndex) return;

    // Получаем внутренний список задач
    // ProjectManager.Tasks возвращает IEnumerable, но внутри скорее всего List
    var tasksField = ProjectManager.GetType()
        .GetField("_tasks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

    if (tasksField?.GetValue(ProjectManager) is System.Collections.IList tasksList)
    {
        if (fromIndex >= 0 && fromIndex < tasksList.Count &&
            toIndex >= 0 && toIndex < tasksList.Count)
        {
            tasksList.Remove(task);

            // Корректируем индекс если удаляли элемент до целевой позиции
            if (fromIndex < toIndex)
                toIndex--;

            tasksList.Insert(toIndex, task);
        }
    }
}

/// <summary>
/// Отменяет операцию перетаскивания.
/// </summary>
private void CancelDrag()
{
    if (_dragState.IsActive && _dragState.Task != null)
    {
        // Восстанавливаем оригинальные значения напрямую
        _dragState.Task.Start = _dragState.OriginalStart;
        _dragState.Task.Duration = _dragState.OriginalDuration;
    }

    FinalizeDrag();
}

/// <summary>
/// Финализирует операцию перетаскивания.
/// </summary>
private void FinalizeDrag()
{
    _dragState.Reset();
    ReleaseMouseCapture();  // На уровне контрола
    Cursor = Cursors.Arrow;
    OverlayLayer.Children.Clear();
    RenderOverlay();
    Render();
}

#endregion
}