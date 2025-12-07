using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Core.Models;
using Core.Services;
using Wpf.Services.Export;

namespace Wpf.Controls;

/// <summary>
/// Визуализация вовлечённости ресурсов по дням.
/// Автоматически обновляется при изменении данных в ResourceService.
/// </summary>
public partial class ResourceEngagementStrip : UserControl
{
    #region Dependency Properties
    
    /// <summary>
    /// Сервис производственного календаря для отображения праздников.
    /// </summary>
    public static readonly DependencyProperty CalendarServiceProperty =
        DependencyProperty.Register(
            nameof(CalendarService),
            typeof(ProductionCalendarService),
            typeof(ResourceEngagementStrip),
            new PropertyMetadata(null, OnCalendarServiceChanged));

    public ProductionCalendarService? CalendarService
    {
        get => (ProductionCalendarService?)GetValue(CalendarServiceProperty);
        set => SetValue(CalendarServiceProperty, value);
    }
    
    private static void OnCalendarServiceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ResourceEngagementStrip strip)
        {
            // Подписываемся/отписываемся от событий
            if (e.OldValue is ProductionCalendarService oldService)
            {
                oldService.HolidaysChanged -= strip.OnHolidaysChanged;
            }
        
            if (e.NewValue is ProductionCalendarService newService)
            {
                newService.HolidaysChanged += strip.OnHolidaysChanged;
            }
        
            strip.Refresh();
        }
    }
    
    private void OnHolidaysChanged(object? sender, EventArgs e)
    {
        Refresh();
    }

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
            typeof(ResourceEngagementStrip), new PropertyMetadata(null, OnResourceServiceChanged));

    public static readonly DependencyProperty EngagementServiceProperty =
        DependencyProperty.Register(nameof(EngagementService), typeof(EngagementCalculationService),
            typeof(ResourceEngagementStrip), new PropertyMetadata(null, OnServiceChanged));

    public static readonly DependencyProperty ProjectManagerProperty =
        DependencyProperty.Register(nameof(ProjectManager), typeof(ProjectManager),
            typeof(ResourceEngagementStrip), new PropertyMetadata(null, OnProjectManagerChanged));

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
    
    /// <summary>
    /// Событие изменения горизонтального скролла (для синхронизации с GanttChart).
    /// </summary>
    public event EventHandler<double>? HorizontalScrollChanged;
    
    /// <summary>
    /// Событие изменения вертикального скролла (для синхронизации с внешней панелью имён).
    /// </summary>
    public event EventHandler<double>? VerticalScrollChanged;

    #endregion

    #region Constants

    private const double RowHeight = 24;
    private const int VisibleDaysBuffer = 5;

    #endregion

    #region Fields

    private bool _isUpdatingScroll;
    private ResourceService? _subscribedResourceService;
    private ProjectManager? _subscribedProjectManager;
    
    private System.Windows.Threading.DispatcherTimer? _zoomDebounceTimer;
    private const int ZoomDebounceMs = 50;

    #endregion

    public ResourceEngagementStrip()
    {
        InitializeComponent();
        
        _zoomDebounceTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(ZoomDebounceMs)
        };
        _zoomDebounceTimer.Tick += (_, _) =>
        {
            _zoomDebounceTimer.Stop();
            Refresh();
        };
        
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Refresh();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Отписываемся от событий при выгрузке контрола
        UnsubscribeFromResourceService();
        UnsubscribeFromProjectManager();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        Refresh();
    }

    private static void OnResourceServiceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ResourceEngagementStrip strip)
        {
            // Отписываемся от старого сервиса
            strip.UnsubscribeFromResourceService();
            
            // Подписываемся на новый
            strip.SubscribeToResourceService();
            
            strip.Refresh();
        }
    }

    private static void OnProjectManagerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ResourceEngagementStrip strip)
        {
            // Отписываемся от старого ProjectManager
            strip.UnsubscribeFromProjectManager();
            
            // Подписываемся на новый
            strip.SubscribeToProjectManager();
            
            strip.Refresh();
        }
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
            // Debounce для плавного зума
            strip._zoomDebounceTimer?.Stop();
            strip._zoomDebounceTimer?.Start();
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
        if (!_isUpdatingScroll)
        {
            _isUpdatingScroll = true;
        
            // Горизонтальная синхронизация
            if (e.HorizontalChange != 0)
            {
                HorizontalOffset = e.HorizontalOffset;
                HorizontalScrollChanged?.Invoke(this, e.HorizontalOffset);
            }
        
            // Вертикальная синхронизация
            if (e.VerticalChange != 0)
            {
                VerticalScrollChanged?.Invoke(this, e.VerticalOffset);
            }
        
            _isUpdatingScroll = false;
        }

        // Внутренняя синхронизация имён (если ShowResourceNames=true)
        NamesScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);
    }

    #endregion

    #region ResourceService Subscription

    private void SubscribeToResourceService()
    {
        if (ResourceService == null || _subscribedResourceService == ResourceService)
            return;

        _subscribedResourceService = ResourceService;

        ResourceService.ResourcesChanged += OnResourceServiceDataChanged;
        ResourceService.AssignmentsChanged += OnResourceServiceDataChanged;
        ResourceService.ParticipationIntervalsChanged += OnResourceServiceDataChanged;
        ResourceService.AbsencesChanged += OnResourceServiceDataChanged;
    }

    private void UnsubscribeFromResourceService()
    {
        if (_subscribedResourceService == null)
            return;

        _subscribedResourceService.ResourcesChanged -= OnResourceServiceDataChanged;
        _subscribedResourceService.AssignmentsChanged -= OnResourceServiceDataChanged;
        _subscribedResourceService.ParticipationIntervalsChanged -= OnResourceServiceDataChanged;
        _subscribedResourceService.AbsencesChanged -= OnResourceServiceDataChanged;

        _subscribedResourceService = null;
    }

    private void OnResourceServiceDataChanged(object? sender, EventArgs e)
    {
        // Обновляем через Dispatcher для thread-safety
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(Refresh));
            return;
        }

        Refresh();
    }

    #endregion

    #region ProjectManager Subscription

    private void SubscribeToProjectManager()
    {
        if (ProjectManager == null || _subscribedProjectManager == ProjectManager)
            return;

        _subscribedProjectManager = ProjectManager;
        ProjectManager.ScheduleChanged += OnProjectManagerScheduleChanged;
    }

    private void UnsubscribeFromProjectManager()
    {
        if (_subscribedProjectManager == null)
            return;

        _subscribedProjectManager.ScheduleChanged -= OnProjectManagerScheduleChanged;
        _subscribedProjectManager = null;
    }

    private void OnProjectManagerScheduleChanged(object? sender, EventArgs e)
    {
        // Обновляем через Dispatcher для thread-safety
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(Refresh));
            return;
        }

        Refresh();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Перерисовывает strip.
    /// </summary>
    public void Refresh()
    {
        if (!IsLoaded)
            return;
    
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

        var (startDay, endDay) = GetVisibleDayRange();
        var totalDays = (int)(endDay - startDay).TotalDays + 1;
        
        EngagementCanvas.Width = totalDays * ColumnWidth;
        EngagementCanvas.Height = resources.Count * RowHeight;

        ResourceNamesPanel.ItemsSource = resources;

        for (int resourceIndex = 0; resourceIndex < resources.Count; resourceIndex++)
        {
            var resource = resources[resourceIndex];
            DrawResourceRow(resource, resourceIndex, startDay, totalDays);
        }

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
            
            // Проверяем, является ли день праздником
            bool isHoliday = CalendarService?.IsHoliday(day) ?? false;

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
            
                // Если день праздник - переопределяем состояние
                if (isHoliday && state != DayState.Weekend && state != DayState.Absence)
                {
                    state = DayState.Holiday;
                    tooltipText = BuildHolidayTooltip(day);
                }
                else
                {
                    tooltipText = state switch
                    {
                        DayState.Weekend => BuildWeekendTooltip(day),
                        DayState.Holiday => BuildHolidayTooltip(day),
                        _ => BuildTooltipText(resource, status)
                    };
                }
            }
            else
            {
                state = ResourceService!.IsResourceAbsent(resource.Id, day) 
                    ? DayState.Absence 
                    : (ResourceService.IsResourceParticipating(resource.Id, day) 
                        ? DayState.Free 
                        : DayState.NotParticipating);
            }

            var cell = CreateCell(state, resource.ColorHex, allocationPercent, maxWorkload, tooltipText);
            Canvas.SetLeft(cell, x);
            Canvas.SetTop(cell, y);
            EngagementCanvas.Children.Add(cell);
        }

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
    
    private string BuildHolidayTooltip(TimeSpan day)
    {
        if (CalendarService == null || ProjectManager == null)
            return "Праздничный день";
    
        var holiday = CalendarService.GetHoliday(day);
        var date = ProjectManager.Start.AddDays(day.Days);
    
        return holiday != null ? $"{holiday.Name}\n{date:dd.MM.yyyy}\nПраздничный день" : $"Праздничный день\n{date:dd.MM.yyyy}";
    }
    
    /// <summary>
    /// Формирует tooltip для выходного дня.
    /// </summary>
    /// <param name="day">День (смещение от начала проекта).</param>
    /// <returns>Текст tooltip.</returns>
    private string BuildWeekendTooltip(TimeSpan day)
    {
        if (ProjectManager != null)
        {
            var actualDate = ProjectManager.Start.AddDays(day.Days);
            var dayName = actualDate.DayOfWeek == DayOfWeek.Saturday ? "Суббота" : "Воскресенье";
            return $"{dayName}, {actualDate:dd.MM.yyyy}\nВыходной день";
        }
    
        return "Выходной день";
    }


    /// </summary>
    /// <param name="resource">Ресурс.</param>
    /// <param name="status">Статус дня.</param>
    /// <returns>Форматированный текст для tooltip.</returns>
    private string BuildTooltipText(Resource resource, DayStatus status)
    {
        var lines = new List<string>();

        // ═══ Заголовок: Имя и роль ═══
        lines.Add(resource.Name);
        lines.Add(resource.Role.GetDisplayName());
        lines.Add("───────────────────────");

        // ═══ Дата ═══
        if (ProjectManager != null)
        {
            var actualDate = ProjectManager.Start.AddDays(status.Day.Days);
            lines.Add($"Дата: {actualDate:dd.MM.yyyy}");
        }
        else
        {
            lines.Add($"День: {status.Day.Days}");
        }

        // ═══ Статус ═══
        lines.Add($"Статус: {status.State.GetDisplayName()}");

        // ═══ Загрузка (только если участвует в проекте) ═══
        if (status.InParticipation)
        {
            lines.Add($"Загрузка: {status.AllocationPercent}% / {status.MaxWorkload}%");
        }

        // ═══ Причина отсутствия ═══
        if (status.InAbsence && !string.IsNullOrWhiteSpace(status.AbsenceReason))
        {
            lines.Add($"Причина: {status.AbsenceReason}");
        }

        // ═══ Назначения (детализация) ═══
        if (status.Assignments.Count > 0 && ProjectManager != null)
        {
            lines.Add("───────────────────────");
            lines.Add("Назначения:");

            var coefficient = resource.Role.GetCoefficient();

            foreach (var assignment in status.Assignments)
            {
                var task = ProjectManager.GetTaskById(assignment.TaskId);
                var taskName = task?.Name ?? $"Задача {assignment.TaskId:N}";
                var contribution = (int)Math.Round(assignment.Workload * coefficient);

                // Формат: • Задача А (100% × 1.00 = 100%)
                lines.Add($"• {taskName} ({assignment.Workload}% × {coefficient:F2} = {contribution}%)");
            }
        }
        else if (status.Assignments.Count > 0)
        {
            // Fallback без ProjectManager
            lines.Add($"Назначений: {status.Assignments.Count}");
        }

        return string.Join(Environment.NewLine, lines);
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

        // Штриховка ТОЛЬКО для отсутствия
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
            DayState.Weekend => new SolidColorBrush(Color.FromRgb(253, 248, 223)), // #EDE0D0 — как в GridRenderer
            DayState.Holiday => new SolidColorBrush(Color.FromRgb(255, 240, 230)),
            DayState.PartialAssigned => CreatePartialBrush(allocation, maxWorkload),
            DayState.Assigned => AssignedBlueBrush,
            DayState.Overbooked => AssignedBlueBrush,
            _ => Brushes.White
        };
    }

    private static Brush CreatePartialBrush(int allocation, int maxWorkload)
    {
        // Вычисляем прозрачность (от 30% до 100%)
        var opacity = maxWorkload > 0 
            ? Math.Min(1.0, 0.3 + 0.7 * allocation / maxWorkload) 
            : 0.3;
    
        // Создаем кисть с тем же цветом, что и AssignedBlueBrush
        return new SolidColorBrush(Color.FromArgb(
            (byte)(255 * opacity), 
            AssignedBlueBrush.Color.R, 
            AssignedBlueBrush.Color.G, 
            AssignedBlueBrush.Color.B));
    }

    
    // Добавьте статическое поле для кисти (вне методов, внутри класса)
    private static readonly SolidColorBrush AssignedBlueBrush = new SolidColorBrush(Color.FromRgb(70, 130, 180)); // SteelBlue
    

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

    #region Export Data Provider

    /// <summary>
    /// Ширина колонки с именами ресурсов при экспорте.
    /// </summary>
    private const double ExportResourceNamesWidth = 160;

    /// <summary>
    /// Возвращает данные полосы загрузки для экспорта.
    /// </summary>
    public EngagementStripExportData? GetExportData()
    {
        if (ResourceService == null || ProjectManager == null)
            return null;

        var resources = ResourceService.Resources.ToList();
        if (resources.Count == 0)
            return null;

        // Убеждаемся, что Canvas отрендерен
        if (EngagementCanvas.ActualWidth <= 0 || EngagementCanvas.ActualHeight <= 0)
        {
            Refresh();
        }

        // Вычисляем единую высоту для обоих канвасов
        var engagementHeight = EngagementCanvas.Height > 0
            ? EngagementCanvas.Height
            : EngagementCanvas.ActualHeight;

        // Создаём Canvas с именами ресурсов (используем ту же высоту, что и EngagementCanvas)
        var namesCanvas = CreateResourceNamesCanvas(resources, engagementHeight);

        return new EngagementStripExportData
        {
            Engagement = new ExportLayerData
            {
                Canvas = EngagementCanvas,
                Width = EngagementCanvas.Width > 0 ? EngagementCanvas.Width : EngagementCanvas.ActualWidth,
                Height = engagementHeight,
                Name = "Engagement"
            },
            ResourceNames = new ExportLayerData
            {
                Canvas = namesCanvas,
                Width = ExportResourceNamesWidth,
                Height = engagementHeight, // Та же высота
                Name = "ResourceNames"
            },
            ColumnWidth = ColumnWidth,
            RowHeight = RowHeight,
            ResourceCount = resources.Count
        };
    }

    /// <summary>
    /// Принудительно обновляет и возвращает данные для экспорта.
    /// Гарантирует, что Canvas содержит актуальные данные.
    /// </summary>
    public EngagementStripExportData? GetExportDataForced()
    {
        // Принудительно перерисовываем
        Refresh();

        // Ждём завершения рендеринга
        Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

        return GetExportData();
    }

    /// <summary>
    /// Создаёт Canvas с именами ресурсов для экспорта.
    /// </summary>
    /// <param name="resources">Список ресурсов.</param>
    /// <param name="totalHeight">Общая высота (должна совпадать с EngagementCanvas).</param>
    private Canvas CreateResourceNamesCanvas(List<Resource> resources, double totalHeight)
    {
        // Вычисляем высоту строки из общей высоты
        var rowHeight = resources.Count > 0 ? totalHeight / resources.Count : RowHeight;

        var canvas = new Canvas
        {
            Width = ExportResourceNamesWidth,
            Height = totalHeight,
            Background = Brushes.White
        };

        for (int i = 0; i < resources.Count; i++)
        {
            var resource = resources[i];
            var y = i * rowHeight;

            // Фон строки (высота как в EngagementCanvas)
            var rowBackground = new Rectangle
            {
                Width = ExportResourceNamesWidth,
                Height = rowHeight - 1, // -1 для линии разделителя, как в EngagementStrip
                Fill = Brushes.White
            };
            Canvas.SetLeft(rowBackground, 0);
            Canvas.SetTop(rowBackground, y);
            canvas.Children.Add(rowBackground);

            // Горизонтальная линия-разделитель (как в EngagementStrip)
            var separatorLine = new Line
            {
                X1 = 0,
                Y1 = y + rowHeight,
                X2 = ExportResourceNamesWidth,
                Y2 = y + rowHeight,
                Stroke = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                StrokeThickness = 1
            };
            canvas.Children.Add(separatorLine);

            // Цветовой индикатор
            var colorIndicator = new Rectangle
            {
                Width = 10,
                Height = 10,
                RadiusX = 2,
                RadiusY = 2,
                Fill = CreateColorBrushFromHex(resource.ColorHex)
            };
            Canvas.SetLeft(colorIndicator, 6);
            Canvas.SetTop(colorIndicator, y + (rowHeight - 10) / 2);
            canvas.Children.Add(colorIndicator);

            // Имя ресурса
            var nameText = new TextBlock
            {
                Text = resource.Name,
                FontSize = 10,
                FontWeight = FontWeights.Normal,
                Foreground = Brushes.Black,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = ExportResourceNamesWidth - 24
            };
            Canvas.SetLeft(nameText, 22);
            Canvas.SetTop(nameText, y + (rowHeight - 14) / 2);
            canvas.Children.Add(nameText);
        }

        // Правая граница колонки
        var borderLine = new Line
        {
            X1 = ExportResourceNamesWidth - 1,
            Y1 = 0,
            X2 = ExportResourceNamesWidth - 1,
            Y2 = totalHeight,
            Stroke = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
            StrokeThickness = 1
        };
        canvas.Children.Add(borderLine);

        return canvas;
    }

    /// <summary>
    /// Создаёт кисть из HEX-цвета.
    /// </summary>
    private static SolidColorBrush CreateColorBrushFromHex(string hex)
    {
        try
        {
            if (string.IsNullOrEmpty(hex))
                return new SolidColorBrush(Color.FromRgb(70, 130, 180));

            if (hex.StartsWith("#"))
                hex = hex.Substring(1);

            var color = Color.FromRgb(
                Convert.ToByte(hex.Substring(0, 2), 16),
                Convert.ToByte(hex.Substring(2, 2), 16),
                Convert.ToByte(hex.Substring(4, 2), 16));

            return new SolidColorBrush(color);
        }
        catch
        {
            return new SolidColorBrush(Color.FromRgb(70, 130, 180));
        }
    }

    #endregion
}