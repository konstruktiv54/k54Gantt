// GanttChart.WPF/Rendering/TaskRenderer.cs
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Core.Services;
using Wpf.Controls;
using Task = Core.Interfaces.Task;

namespace Wpf.Rendering;

/// <summary>
/// Рендерер баров задач.
/// Отображает обычные задачи, групповые задачи и прогресс выполнения.
/// </summary>
public class TaskRenderer
{
    private readonly GanttChartControl _control;
    private ResourceService? _resourceService;

    // Кэшированные кисти
    private Brush? _taskBarBrush;
    private Brush? _taskBarProgressBrush;
    private Brush? _taskBarBorderBrush;
    private Brush? _groupTaskBrush;
    private Brush? _textBrush;

    public TaskRenderer(GanttChartControl control)
    {
        _control = control;
    }
    
    public void SetResourceService(ResourceService resourceService)
    {
        _resourceService = resourceService;
    }

    /// <summary>
    /// Рендерит все задачи на указанном Canvas.
    /// </summary>
    public void Render(Canvas canvas)
    {
        if (_control.ProjectManager == null)
            return;

        InitializeResources();

        var tasks = _control.ProjectManager.Tasks;
        var rowIndex = 0;

        foreach (var task in tasks)
        {
            // Пропускаем скрытые задачи (дочерние свёрнутых групп)
            if (IsTaskHidden(task))
            {
                continue;
            }

            RenderTask(canvas, task, rowIndex);
            rowIndex++;
        }
    }

    private void InitializeResources()
    {
        _taskBarBrush ??= _control.FindResource("TaskBarBrush") as Brush ?? Brushes.SteelBlue;
        _taskBarProgressBrush ??= _control.FindResource("TaskBarProgressBrush") as Brush ?? Brushes.DarkSlateBlue;
        _taskBarBorderBrush ??= _control.FindResource("TaskBarBorderBrush") as Brush ?? Brushes.DarkBlue;
        _groupTaskBrush ??= _control.FindResource("GroupTaskBrush") as Brush ?? Brushes.DimGray;
        _textBrush ??= Brushes.White;
    }

    private bool IsTaskHidden(Task task)
    {
        if (_control.ProjectManager == null)
            return false;

        // Проверяем, находится ли задача в свёрнутой группе
        foreach (var group in _control.ProjectManager.GroupsOf(task))
        {
            if (group.IsCollapsed)
                return true;
        }

        return false;
    }

    private void RenderTask(Canvas canvas, Task task, int rowIndex)
    {
        var columnWidth = _control.ColumnWidth;
        var barHeight = _control.BarHeight;
        var barSpacing = _control.BarSpacing;
        var rowHeight = _control.RowHeight;

        // Позиция бара
        var x = task.Start.Days * columnWidth;
        var y = rowIndex * rowHeight + barSpacing / 2;
        var width = Math.Max(task.Duration.Days * columnWidth, columnWidth * 0.5);

        // Проверяем тип задачи
        var isGroup = _control.ProjectManager!.IsGroup(task);
        var isSplit = _control.ProjectManager.IsSplit(task);

        if (isGroup)
        {
            RenderGroupTask(canvas, task, x, y, width, barHeight);
        }
        else if (isSplit)
        {
            RenderSplitTask(canvas, task, rowIndex);
        }
        else
        {
            RenderRegularTask(canvas, task, x, y, width, barHeight);
        }

        // Имя задачи (справа от бара или внутри)
        RenderTaskName(canvas, task, x, y, width, barHeight);
    }

    private void RenderRegularTask(Canvas canvas, Task task, double x, double y, double width, double height)
    {
        // Основной бар
        var taskBar = new Rectangle
        {
            Width = width,
            Height = height,
            Fill = _taskBarBrush,
            Stroke = _taskBarBorderBrush,
            StrokeThickness = 1,
            RadiusX = 3,
            RadiusY = 3
        };

        Canvas.SetLeft(taskBar, x);
        Canvas.SetTop(taskBar, y);
        canvas.Children.Add(taskBar);

        // Прогресс
        if (task.Complete > 0)
        {
            var progressWidth = width * task.Complete;

            var progressBar = new Rectangle
            {
                Width = Math.Max(progressWidth, 2),
                Height = height,
                Fill = _taskBarProgressBrush,
                RadiusX = 3,
                RadiusY = 3,
                Clip = new RectangleGeometry(new Rect(0, 0, progressWidth, height), 3, 3)
            };

            Canvas.SetLeft(progressBar, x);
            Canvas.SetTop(progressBar, y);
            canvas.Children.Add(progressBar);
        }

        // Процент выполнения (внутри бара, если достаточно места)
        if (width >= 40 && task.Complete > 0 && task.Complete < 1)
        {
            var percentText = new TextBlock
            {
                Text = $"{(int)(task.Complete * 100)}%",
                FontSize = 9,
                Foreground = _textBrush,
                FontWeight = FontWeights.SemiBold
            };

            percentText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            
            Canvas.SetLeft(percentText, x + (width - percentText.DesiredSize.Width) / 2);
            Canvas.SetTop(percentText, y + (height - percentText.DesiredSize.Height) / 2);
            canvas.Children.Add(percentText);
        }
    }

    private void RenderGroupTask(Canvas canvas, Task task, double x, double y, double width, double height)
    {
        // Групповая задача отображается как "скобка" с уголками
        var groupHeight = height * 0.4;
        var bracketHeight = height * 0.3;

        // Основная линия
        var mainLine = new Rectangle
        {
            Width = width,
            Height = groupHeight,
            Fill = _groupTaskBrush
        };

        Canvas.SetLeft(mainLine, x);
        Canvas.SetTop(mainLine, y + (height - groupHeight) / 2);
        canvas.Children.Add(mainLine);

        // Левая скобка (уголок вниз)
        var leftBracket = new Polygon
        {
            Points = new PointCollection
            {
                new Point(0, 0),
                new Point(8, 0),
                new Point(0, bracketHeight)
            },
            Fill = _groupTaskBrush
        };

        Canvas.SetLeft(leftBracket, x);
        Canvas.SetTop(leftBracket, y + (height - groupHeight) / 2 + groupHeight);
        canvas.Children.Add(leftBracket);

        // Правая скобка (уголок вниз)
        var rightBracket = new Polygon
        {
            Points = new PointCollection
            {
                new Point(8, 0),
                new Point(0, 0),
                new Point(8, bracketHeight)
            },
            Fill = _groupTaskBrush
        };

        Canvas.SetLeft(rightBracket, x + width - 8);
        Canvas.SetTop(rightBracket, y + (height - groupHeight) / 2 + groupHeight);
        canvas.Children.Add(rightBracket);

        // Иконка сворачивания (± )
        var collapseIcon = task.IsCollapsed ? "+" : "−";
        var iconText = new TextBlock
        {
            Text = collapseIcon,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White
        };

        iconText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(iconText, x + 2);
        Canvas.SetTop(iconText, y + (height - iconText.DesiredSize.Height) / 2 - 2);
        canvas.Children.Add(iconText);
    }

    private void RenderSplitTask(Canvas canvas, Task splitTask, int rowIndex)
    {
        var parts = _control.ProjectManager!.PartsOf(splitTask).ToList();
        
        foreach (var part in parts)
        {
            var x = part.Start.Days * _control.ColumnWidth;
            var y = rowIndex * _control.RowHeight + _control.BarSpacing / 2;
            var width = Math.Max(part.Duration.Days * _control.ColumnWidth, _control.ColumnWidth * 0.5);

            // Рисуем часть как обычный бар, но с пунктирной границей
            var partBar = new Rectangle
            {
                Width = width,
                Height = _control.BarHeight,
                Fill = _taskBarBrush,
                Stroke = _taskBarBorderBrush,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 3, 1 },
                RadiusX = 3,
                RadiusY = 3
            };

            Canvas.SetLeft(partBar, x);
            Canvas.SetTop(partBar, y);
            canvas.Children.Add(partBar);

            // Прогресс части
            if (part.Complete > 0)
            {
                var progressWidth = width * part.Complete;
                var progressBar = new Rectangle
                {
                    Width = Math.Max(progressWidth, 2),
                    Height = _control.BarHeight,
                    Fill = _taskBarProgressBrush,
                    RadiusX = 3,
                    RadiusY = 3
                };

                Canvas.SetLeft(progressBar, x);
                Canvas.SetTop(progressBar, y);
                canvas.Children.Add(progressBar);
            }
        }

        // Пунктирная линия между частями
        if (parts.Count > 1)
        {
            for (int i = 0; i < parts.Count - 1; i++)
            {
                var currentPart = parts[i];
                var nextPart = parts[i + 1];

                var lineX1 = currentPart.End.Days * _control.ColumnWidth;
                var lineX2 = nextPart.Start.Days * _control.ColumnWidth;
                var lineY = rowIndex * _control.RowHeight + _control.BarSpacing / 2 + _control.BarHeight / 2;

                var connector = new Line
                {
                    X1 = lineX1,
                    Y1 = lineY,
                    X2 = lineX2,
                    Y2 = lineY,
                    Stroke = _taskBarBorderBrush,
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 2, 2 }
                };

                canvas.Children.Add(connector);
            }
        }
    }

    private void RenderTaskName(Canvas canvas, Task task, double x, double y, double width, double height)
    {
        // Инициалы ресурсов НАД баром
        RenderResourceInitials(canvas, task, x, y, width);

        // Имя задачи справа от бара
        var name = task.Name ?? "Без названия";
    
        if (name.Length > 30)
            name = name.Substring(0, 27) + "...";

        var nameText = new TextBlock
        {
            Text = name,
            FontSize = 11,
            Foreground = _control.FindResource("TextPrimaryBrush") as Brush ?? Brushes.Black
        };

        nameText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var textX = x + width + 8;
        var textY = y + (height - nameText.DesiredSize.Height) / 2;

        Canvas.SetLeft(nameText, textX);
        Canvas.SetTop(nameText, textY);
        canvas.Children.Add(nameText);
    }
    
    private void RenderResourceInitials(Canvas canvas, Task task, double x, double y, double width)
    {
        if (_resourceService == null)
            return;

        var initials = _resourceService.GetInitialsForTask(task.Id, ", ");
    
        if (string.IsNullOrEmpty(initials))
            return;

        // Получаем ресурсы для определения цвета (берём цвет первого ресурса)
        var resources = _resourceService.GetResourcesForTask(task.Id).ToList();
        Brush textBrush = _control.FindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray;

        if (resources.Count > 0 && !string.IsNullOrEmpty(resources[0].ColorHex))
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(resources[0].ColorHex);
                textBrush = new SolidColorBrush(color);
            }
            catch
            {
                // Используем цвет по умолчанию
            }
        }

        var initialsText = new TextBlock
        {
            Text = initials,
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
            Foreground = textBrush
        };

        initialsText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        // Позиционируем над баром по центру
        var textX = x + (width - initialsText.DesiredSize.Width) / 2;
        var textY = y - initialsText.DesiredSize.Height - 2;

        // Не показываем если выходит за верхнюю границу
        if (textY < 0)
            textY = y - 12; // Показываем чуть выше если места совсем мало

        Canvas.SetLeft(initialsText, Math.Max(x, textX));
        Canvas.SetTop(initialsText, Math.Max(0, textY));
        canvas.Children.Add(initialsText);
    }
}