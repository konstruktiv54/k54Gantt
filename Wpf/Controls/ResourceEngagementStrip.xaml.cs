using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Core.Models;
using Core.Services;

namespace Wpf.Controls;

/// <summary>
/// Визуализация вовлечённости ресурсов по дням.
/// </summary>
public partial class ResourceEngagementStrip : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty ShowResourceNamesProperty =
        DependencyProperty.Register(nameof(ShowResourceNames), typeof(bool),
            typeof(ResourceEngagementStrip), new PropertyMetadata(true));

    public bool ShowResourceNames
    {
        get => (bool)GetValue(ShowResourceNamesProperty);
        set => SetValue(ShowResourceNamesProperty, value);
    }
    
    public static readonly DependencyProperty ResourceServiceProperty =
        DependencyProperty.Register(nameof(ResourceService), typeof(ResourceService),
            typeof(ResourceEngagementStrip), new PropertyMetadata(null, OnServiceChanged));

    public static readonly DependencyProperty EngagementServiceProperty =
        DependencyProperty.Register(nameof(EngagementService), typeof(EngagementCalculationService),
            typeof(ResourceEngagementStrip), new PropertyMetadata(null, OnServiceChanged));

    public static readonly DependencyProperty ProjectManagerProperty =
        DependencyProperty.Register(nameof(ProjectManager), typeof(ProjectManager),
            typeof(ResourceEngagementStrip), new PropertyMetadata(null, OnServiceChanged));

    public static readonly DependencyProperty ColumnWidthProperty =
        DependencyProperty.Register(nameof(ColumnWidth), typeof(double),
            typeof(ResourceEngagementStrip), new PropertyMetadata(40.0, OnLayoutPropertyChanged));

    public static readonly DependencyProperty HorizontalOffsetProperty =
        DependencyProperty.Register(nameof(HorizontalOffset), typeof(double),
            typeof(ResourceEngagementStrip), new PropertyMetadata(0.0, OnHorizontalOffsetChanged));

    public ResourceService? ResourceService
    {
        get => (ResourceService?)GetValue(ResourceServiceProperty);
        set => SetValue(ResourceServiceProperty, value);
    }

    public EngagementCalculationService? EngagementService
    {
        get => (EngagementCalculationService?)GetValue(EngagementServiceProperty);
        set => SetValue(EngagementServiceProperty, value);
    }

    public ProjectManager? ProjectManager
    {
        get => (ProjectManager?)GetValue(ProjectManagerProperty);
        set => SetValue(ProjectManagerProperty, value);
    }

    public double ColumnWidth
    {
        get => (double)GetValue(ColumnWidthProperty);
        set => SetValue(ColumnWidthProperty, value);
    }

    public double HorizontalOffset
    {
        get => (double)GetValue(HorizontalOffsetProperty);
        set => SetValue(HorizontalOffsetProperty, value);
    }

    #endregion

    #region Events

    public event EventHandler<Resource>? ResourceDoubleClicked;

    #endregion

    #region Constants

    private const double RowHeight = 24;
    private const int VisibleDaysBuffer = 5; // Дополнительные дни за пределами viewport

    #endregion

    #region Fields

    private bool _isUpdatingScroll;

    #endregion

    public ResourceEngagementStrip()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Refresh();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        Refresh();
    }

    private static void OnServiceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ResourceEngagementStrip strip)
        {
            strip.Refresh();
        }
    }

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ResourceEngagementStrip strip)
        {
            strip.Dispatcher.BeginInvoke(() =>
            {
                strip.Refresh();
            }, System.Windows.Threading.DispatcherPriority.Background);
        }
    }

    private static void OnHorizontalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ResourceEngagementStrip strip && !strip._isUpdatingScroll)
        {
            strip._isUpdatingScroll = true;
            strip.TimelineScrollViewer.ScrollToHorizontalOffset((double)e.NewValue);
            strip._isUpdatingScroll = false;
        }
    }

    private void TimelineScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (!_isUpdatingScroll && e.HorizontalChange != 0)
        {
            _isUpdatingScroll = true;
            HorizontalOffset = e.HorizontalOffset;
            _isUpdatingScroll = false;
        }

        // Синхронизируем вертикальный скролл имён
        NamesScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Перерисовывает strip.
    /// </summary>
    public void Refresh()
    {
        Debug.WriteLine($"Refresh called. ColumnWidth: {ColumnWidth}, ActualWidth: {ActualWidth}");
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(Refresh));
            return;
        }
        
        EngagementCanvas.Children.Clear();

        if (ResourceService == null || ProjectManager == null)
            return;

        var resources = ResourceService.Resources.ToList();
        if (resources.Count == 0)
            return;

        // Определяем диапазон дней
        var (startDay, endDay) = GetVisibleDayRange();
        var totalDays = (int)(endDay - startDay).TotalDays + 1;

        // Устанавливаем размер Canvas
        EngagementCanvas.Width = totalDays * ColumnWidth;
        EngagementCanvas.Height = resources.Count * RowHeight;

        // Обновляем DataContext для имён
        ResourceNamesPanel.ItemsSource = resources;

        // Рисуем ячейки
        for (int resourceIndex = 0; resourceIndex < resources.Count; resourceIndex++)
        {
            var resource = resources[resourceIndex];
            DrawResourceRow(resource, resourceIndex, startDay, totalDays);
        }

        // Рисуем вертикальные линии сетки
        DrawGridLines(totalDays, resources.Count);
    }

    #endregion

    #region Private Methods

    private (TimeSpan start, TimeSpan end) GetVisibleDayRange()
    {
        if (ProjectManager?.Tasks == null || ProjectManager.Tasks.Count == 0)
        {
            return (TimeSpan.Zero, TimeSpan.FromDays(30));
        }

        var minStart = ProjectManager.Tasks.Min(t => t.Start);
        var maxEnd = ProjectManager.Tasks.Max(t => t.End);

        // Добавляем buffer
        minStart -= TimeSpan.FromDays(VisibleDaysBuffer);
        maxEnd += TimeSpan.FromDays(VisibleDaysBuffer);

        if (minStart < TimeSpan.Zero)
            minStart = TimeSpan.Zero;

        return (minStart, maxEnd);
    }

    private void DrawResourceRow(Resource resource, int rowIndex, TimeSpan startDay, int totalDays)
    {
        var y = rowIndex * RowHeight;

        for (int dayOffset = 0; dayOffset < totalDays; dayOffset++)
        {
            var day = startDay + TimeSpan.FromDays(dayOffset);
            var x = dayOffset * ColumnWidth;

            // Получаем статус дня
            DayState state;
            int allocationPercent = 0;
            int maxWorkload = 100;
            string tooltipText = "";

            if (EngagementService != null)
            {
                var status = EngagementService.GetDayStatus(resource.Id, day);
                state = status.State;
                allocationPercent = status.AllocationPercent;
                maxWorkload = status.MaxWorkload;
                tooltipText = status.TooltipText;
            }
            else
            {
                // Fallback без EngagementService
                state = ResourceService!.IsResourceAbsent(resource.Id, day) 
                    ? DayState.Absence 
                    : (ResourceService.IsResourceParticipating(resource.Id, day) 
                        ? DayState.Free 
                        : DayState.NotParticipating);
            }

            // Создаём ячейку
            var cell = CreateCell(state, resource.ColorHex, allocationPercent, maxWorkload, tooltipText);
            Canvas.SetLeft(cell, x);
            Canvas.SetTop(cell, y);
            EngagementCanvas.Children.Add(cell);
        }

        // Горизонтальная линия под строкой
        var line = new Line
        {
            X1 = 0,
            Y1 = y + RowHeight,
            X2 = totalDays * ColumnWidth,
            Y2 = y + RowHeight,
            Stroke = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
            StrokeThickness = 1
        };
        EngagementCanvas.Children.Add(line);
    }

    private Rectangle CreateCell(DayState state, string colorHex, int allocation, int maxWorkload, string tooltip)
    {
        var cell = new Rectangle
        {
            Width = ColumnWidth - 1,
            Height = RowHeight - 1,
            Fill = GetCellBrush(state, colorHex, allocation, maxWorkload),
            Stroke = state == DayState.Overbooked 
                ? new SolidColorBrush(Color.FromRgb(211, 47, 47)) 
                : null,
            StrokeThickness = state == DayState.Overbooked ? 2 : 0,
            ToolTip = string.IsNullOrEmpty(tooltip) ? null : tooltip
        };

        // Штриховка для Absence
        if (state == DayState.Absence)
        {
            cell.Fill = CreateHatchBrush();
        }

        return cell;
    }

    private static Brush GetCellBrush(DayState state, string colorHex, int allocation, int maxWorkload)
    {
        return state switch
        {
            DayState.Free => new SolidColorBrush(Color.FromRgb(250, 250, 250)),
            DayState.Absence => new SolidColorBrush(Color.FromRgb(189, 189, 189)),
            DayState.NotParticipating => new SolidColorBrush(Color.FromRgb(238, 238, 238)),
            DayState.PartialAssigned => CreatePartialBrush(colorHex, allocation, maxWorkload),
            DayState.Assigned => CreateColorBrush(colorHex, 1.0),
            DayState.Overbooked => CreateColorBrush(colorHex, 1.0),
            _ => Brushes.White
        };
    }

    private static Brush CreatePartialBrush(string colorHex, int allocation, int maxWorkload)
    {
        var opacity = maxWorkload > 0 
            ? Math.Min(1.0, 0.3 + 0.7 * allocation / maxWorkload) 
            : 0.3;
        return CreateColorBrush(colorHex, opacity);
    }

    private static SolidColorBrush CreateColorBrush(string hex, double opacity)
    {
        try
        {
            if (hex.StartsWith("#"))
                hex = hex.Substring(1);

            var color = Color.FromRgb(
                Convert.ToByte(hex.Substring(0, 2), 16),
                Convert.ToByte(hex.Substring(2, 2), 16),
                Convert.ToByte(hex.Substring(4, 2), 16));
            
            color.A = (byte)(255 * opacity);
            return new SolidColorBrush(color);
        }
        catch
        {
            return new SolidColorBrush(Color.FromArgb((byte)(255 * opacity), 70, 130, 180));
        }
    }

    private static DrawingBrush CreateHatchBrush()
    {
        var geometry = new GeometryGroup();
        geometry.Children.Add(new LineGeometry(new Point(0, 0), new Point(8, 8)));

        var drawing = new GeometryDrawing
        {
            Geometry = geometry,
            Pen = new Pen(new SolidColorBrush(Color.FromRgb(150, 150, 150)), 1)
        };

        return new DrawingBrush
        {
            Drawing = drawing,
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 8, 8),
            ViewportUnits = BrushMappingMode.Absolute
        };
    }

    private void DrawGridLines(int totalDays, int resourceCount)
    {
        for (int i = 0; i <= totalDays; i++)
        {
            var x = i * ColumnWidth;
            var line = new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = resourceCount * RowHeight,
                Stroke = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                StrokeThickness = 1
            };
            EngagementCanvas.Children.Add(line);
        }
    }

    #endregion
}