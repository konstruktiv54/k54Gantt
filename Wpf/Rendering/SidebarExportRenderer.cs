// Wpf/Services/Export/SidebarExportRenderer.cs

using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Wpf.ViewModels;

namespace Wpf.Services.Export;

/// <summary>
/// Рендерер Sidebar (DataGrid) для экспорта в XPS.
/// Создаёт Canvas программно, так как DataGrid виртуализирован.
/// </summary>
public static class SidebarExportRenderer
{
    #region Constants
    
    /// <summary>
    /// Ширина Sidebar.
    /// </summary>
    public const double SidebarWidth = 400;
    
    /// <summary>
    /// Высота заголовка.
    /// </summary>
    public const double HeaderHeight = 50;
    
    /// <summary>
    /// Высота строки.
    /// </summary>
    public const double RowHeight = 28;
    
    /// <summary>
    /// Отступ на уровень иерархии.
    /// </summary>
    public const double IndentSize = 16;
    
    // Ширины колонок
    private const double TaskColumnWidth = 120;
    private const double StartColumnWidth = 60;
    private const double DaysColumnWidth = 60;
    private const double EndColumnWidth = 60;
    private const double PercentColumnWidth = 40;
    private const double DeadlineColumnWidth = 60;
    
    #endregion
    
    #region Colors
    
    private static readonly Brush HeaderBackgroundBrush = new SolidColorBrush(Color.FromRgb(245, 245, 245));
    private static readonly Brush HeaderTextBrush = new SolidColorBrush(Color.FromRgb(33, 33, 33));
    private static readonly Brush RowBackgroundBrush = Brushes.White;
    private static readonly Brush RowAlternateBackgroundBrush = new SolidColorBrush(Color.FromRgb(250, 250, 250));
    private static readonly Brush TextPrimaryBrush = new SolidColorBrush(Color.FromRgb(33, 33, 33));
    private static readonly Brush TextSecondaryBrush = new SolidColorBrush(Color.FromRgb(117, 117, 117));
    private static readonly Brush GridLineBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224));
    private static readonly Brush OverdueBrush = new SolidColorBrush(Color.FromRgb(211, 47, 47));
    private static readonly Brush GroupTextBrush = new SolidColorBrush(Color.FromRgb(33, 33, 33));
    
    #endregion
    
    #region Column Definitions
    
    private static readonly ColumnDefinition[] Columns =
    {
        new("Задача", TaskColumnWidth, HorizontalAlignment.Left),
        new("Старт", StartColumnWidth, HorizontalAlignment.Center),
        new("Дней\n(рабоч.)", DaysColumnWidth, HorizontalAlignment.Center),
        new("Финиш", EndColumnWidth, HorizontalAlignment.Center),
        new("%", PercentColumnWidth, HorizontalAlignment.Center),
        new("Дедлайн", DeadlineColumnWidth, HorizontalAlignment.Center)
    };
    
    private record ColumnDefinition(string Header, double Width, HorizontalAlignment Alignment);
    
    #endregion
    
    /// <summary>
    /// Создаёт данные Sidebar для экспорта.
    /// </summary>
    /// <param name="tasks">Плоский список задач для отображения.</param>
    /// <param name="rowsHeight">Высота области строк (должна совпадать с GanttChart).</param>
    /// <returns>Данные для экспорта или null, если задач нет.</returns>
    public static SidebarExportData? CreateExportData(
        ObservableCollection<TaskItemViewModel>? tasks,
        double rowsHeight)
    {
        if (tasks == null || tasks.Count == 0)
            return null;
        
        var headerCanvas = CreateHeaderCanvas();
        var rowsCanvas = CreateRowsCanvas(tasks, rowsHeight);
        
        return new SidebarExportData
        {
            Header = new ExportLayerData
            {
                Canvas = headerCanvas,
                Width = SidebarWidth,
                Height = HeaderHeight,
                Name = "SidebarHeader"
            },
            Rows = new ExportLayerData
            {
                Canvas = rowsCanvas,
                Width = SidebarWidth,
                Height = rowsHeight,
                Name = "SidebarRows"
            },
            Width = SidebarWidth
        };
    }
    
    #region Header Rendering
    
    /// <summary>
    /// Создаёт Canvas с заголовками колонок.
    /// </summary>
    private static Canvas CreateHeaderCanvas()
    {
        var canvas = new Canvas
        {
            Width = SidebarWidth,
            Height = HeaderHeight,
            Background = HeaderBackgroundBrush
        };
        
        double x = 0;
        
        foreach (var column in Columns)
        {
            // Фон ячейки заголовка
            var cellBorder = new Rectangle
            {
                Width = column.Width,
                Height = HeaderHeight,
                Fill = HeaderBackgroundBrush,
                Stroke = GridLineBrush,
                StrokeThickness = 0.5
            };
            Canvas.SetLeft(cellBorder, x);
            Canvas.SetTop(cellBorder, 0);
            canvas.Children.Add(cellBorder);
            
            // Текст заголовка
            var headerText = new TextBlock
            {
                Text = column.Header,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = HeaderTextBrush,
                TextAlignment = TextAlignment.Center,
                Width = column.Width - 8,
                TextWrapping = TextWrapping.Wrap
            };
            
            headerText.Measure(new Size(column.Width - 8, HeaderHeight));
            var textY = (HeaderHeight - headerText.DesiredSize.Height) / 2;
            
            Canvas.SetLeft(headerText, x + 4);
            Canvas.SetTop(headerText, textY);
            canvas.Children.Add(headerText);
            
            x += column.Width;
        }
        
        // Нижняя граница заголовка
        var bottomLine = new Line
        {
            X1 = 0,
            Y1 = HeaderHeight - 0.5,
            X2 = SidebarWidth,
            Y2 = HeaderHeight - 0.5,
            Stroke = GridLineBrush,
            StrokeThickness = 1
        };
        canvas.Children.Add(bottomLine);
        
        return canvas;
    }
    
    #endregion
    
    #region Rows Rendering
    
    /// <summary>
    /// Создаёт Canvas со строками задач.
    /// </summary>
    private static Canvas CreateRowsCanvas(
        ObservableCollection<TaskItemViewModel> tasks,
        double totalHeight)
    {
        var canvas = new Canvas
        {
            Width = SidebarWidth,
            Height = totalHeight,
            Background = RowBackgroundBrush
        };
        
        double y = 0;
        int rowIndex = 0;
        
        foreach (var task in tasks)
        {
            RenderRow(canvas, task, y, rowIndex % 2 == 1);
            y += RowHeight;
            rowIndex++;
        }
        
        // Вертикальные разделители колонок
        RenderColumnSeparators(canvas, totalHeight);
        
        return canvas;
    }
    
    /// <summary>
    /// Рендерит одну строку задачи.
    /// </summary>
    private static void RenderRow(Canvas canvas, TaskItemViewModel task, double y, bool isAlternate)
    {
        double x = 0;
        
        // Фон строки
        var rowBackground = new Rectangle
        {
            Width = SidebarWidth,
            Height = RowHeight,
            Fill = isAlternate ? RowAlternateBackgroundBrush : RowBackgroundBrush
        };
        Canvas.SetLeft(rowBackground, 0);
        Canvas.SetTop(rowBackground, y);
        canvas.Children.Add(rowBackground);
        
        // Горизонтальная линия под строкой
        var rowLine = new Line
        {
            X1 = 0,
            Y1 = y + RowHeight - 0.5,
            X2 = SidebarWidth,
            Y2 = y + RowHeight - 0.5,
            Stroke = GridLineBrush,
            StrokeThickness = 0.5
        };
        canvas.Children.Add(rowLine);
        
        // 1. Колонка "Задача" (с отступом и иконкой)
        RenderTaskNameCell(canvas, task, x, y);
        x += TaskColumnWidth;
        
        // 2. Колонка "Старт"
        RenderTextCell(canvas, task.StartDate.ToString("dd.MM.yy"), x, y, StartColumnWidth, HorizontalAlignment.Center);
        x += StartColumnWidth;
        
        // 3. Колонка "Дней (рабоч.)"
        RenderTextCell(canvas, task.DaysDisplay, x, y, DaysColumnWidth, HorizontalAlignment.Center);
        x += DaysColumnWidth;
        
        // 4. Колонка "Финиш"
        RenderTextCell(canvas, task.EndDate.ToString("dd.MM.yy"), x, y, EndColumnWidth, HorizontalAlignment.Center);
        x += EndColumnWidth;
        
        // 5. Колонка "%"
        RenderTextCell(canvas, $"{task.CompletePercent}%", x, y, PercentColumnWidth, HorizontalAlignment.Center);
        x += PercentColumnWidth;
        
        // 6. Колонка "Дедлайн"
        RenderDeadlineCell(canvas, task, x, y);
    }
    
    /// <summary>
    /// Рендерит ячейку с названием задачи (с отступом и иконкой).
    /// </summary>
    private static void RenderTaskNameCell(Canvas canvas, TaskItemViewModel task, double x, double y)
    {
        var indent = task.Level * IndentSize;
        var iconWidth = 16.0;
        var iconMargin = 4.0;
        var textX = x + 4 + indent + iconWidth + iconMargin;
        var availableWidth = TaskColumnWidth - 4 - indent - iconWidth - iconMargin - 4;
        
        // Иконка (●, 📁, ⇆)
        var iconText = task.IsGroup ? "📁" : task.IsSplitRoot ? "⇆" : "●";
        var icon = new TextBlock
        {
            Text = iconText,
            FontSize = task.IsGroup || task.IsSplitRoot ? 12 : 8,
            Foreground = TextSecondaryBrush
        };
        
        icon.Measure(new Size(iconWidth, RowHeight));
        Canvas.SetLeft(icon, x + 4 + indent);
        Canvas.SetTop(icon, y + (RowHeight - icon.DesiredSize.Height) / 2);
        canvas.Children.Add(icon);
        
        // Название задачи
        var name = task.Name ?? "Без названия";
        var nameText = new TextBlock
        {
            Text = name,
            FontSize = 11,
            FontWeight = task.IsGroup ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = task.IsGroup ? GroupTextBrush : TextPrimaryBrush,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = availableWidth
        };
        
        nameText.Measure(new Size(availableWidth, RowHeight));
        Canvas.SetLeft(nameText, textX);
        Canvas.SetTop(nameText, y + (RowHeight - nameText.DesiredSize.Height) / 2);
        canvas.Children.Add(nameText);
    }
    
    /// <summary>
    /// Рендерит текстовую ячейку.
    /// </summary>
    private static void RenderTextCell(
        Canvas canvas, 
        string text, 
        double x, 
        double y, 
        double width, 
        HorizontalAlignment alignment)
    {
        var textBlock = new TextBlock
        {
            Text = text,
            FontSize = 11,
            Foreground = TextPrimaryBrush,
            TextAlignment = alignment == HorizontalAlignment.Center ? TextAlignment.Center : TextAlignment.Left,
            Width = width - 8
        };
        
        textBlock.Measure(new Size(width - 8, RowHeight));
        
        var textX = alignment switch
        {
            HorizontalAlignment.Center => x + (width - textBlock.DesiredSize.Width) / 2,
            HorizontalAlignment.Right => x + width - textBlock.DesiredSize.Width - 4,
            _ => x + 4
        };
        
        Canvas.SetLeft(textBlock, textX);
        Canvas.SetTop(textBlock, y + (RowHeight - textBlock.DesiredSize.Height) / 2);
        canvas.Children.Add(textBlock);
    }
    
    /// <summary>
    /// Рендерит ячейку дедлайна с учётом просрочки.
    /// </summary>
    private static void RenderDeadlineCell(Canvas canvas, TaskItemViewModel task, double x, double y)
    {
        string text;
        Brush foreground;
        FontWeight fontWeight;
        
        if (task.HasDeadline)
        {
            text = task.DeadlineDate!.Value.ToString("dd.MM.yy");
            foreground = task.IsOverdue ? OverdueBrush : TextPrimaryBrush;
            fontWeight = task.IsOverdue ? FontWeights.SemiBold : FontWeights.Normal;
        }
        else
        {
            text = "—";
            foreground = TextSecondaryBrush;
            fontWeight = FontWeights.Normal;
        }
        
        var textBlock = new TextBlock
        {
            Text = text,
            FontSize = 11,
            Foreground = foreground,
            FontWeight = fontWeight,
            TextAlignment = TextAlignment.Center,
            Width = DeadlineColumnWidth - 8
        };
        
        textBlock.Measure(new Size(DeadlineColumnWidth - 8, RowHeight));
        Canvas.SetLeft(textBlock, x + 4);
        Canvas.SetTop(textBlock, y + (RowHeight - textBlock.DesiredSize.Height) / 2);
        canvas.Children.Add(textBlock);
    }
    
    /// <summary>
    /// Рендерит вертикальные разделители между колонками.
    /// </summary>
    private static void RenderColumnSeparators(Canvas canvas, double height)
    {
        double x = 0;
        
        foreach (var column in Columns)
        {
            x += column.Width;
            
            if (x < SidebarWidth) // Не рисуем после последней колонки
            {
                var separator = new Line
                {
                    X1 = x - 0.5,
                    Y1 = 0,
                    X2 = x - 0.5,
                    Y2 = height,
                    Stroke = GridLineBrush,
                    StrokeThickness = 0.5
                };
                canvas.Children.Add(separator);
            }
        }
    }
    
    #endregion
}