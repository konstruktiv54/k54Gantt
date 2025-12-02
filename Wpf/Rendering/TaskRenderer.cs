// GanttChart.WPF/Rendering/TaskRenderer.cs
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
    
    // Ширина области для отображения ресурсов слева от бара
    private const double ResourceAreaWidth = 80.0;
    
    // Отступ между областью ресурсов и баром
    private const double ResourceGap = 4.0;

    // Кэшированные кисти
    private Brush? _taskBarBrush;
    private Brush? _taskBarProgressBrush;
    private Brush? _taskBarBorderBrush;
    private Brush? _groupTaskBrush;
    private Brush? _textBrush;
    private Brush? _taskCompletedBrush;      // Зелёный для 100%
    private Brush? _deadlineBrush;           // Красный для deadline
    private Brush? _noteBrush;               // Серый для заметок

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
        
        _taskCompletedBrush ??= _control.TryFindResource("TaskCompletedBrush") as Brush 
                                ?? new SolidColorBrush(Color.FromRgb(76, 175, 80));  // Green 500
        _deadlineBrush ??= _control.TryFindResource("DeadlineBrush") as Brush 
                           ?? new SolidColorBrush(Color.FromRgb(244, 67, 54));       // Red 500
        _noteBrush ??= _control.TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray;
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

        // Deadline (красная стена) — ПОСЛЕ бара, ПЕРЕД текстом
        RenderDeadline(canvas, task, y, barHeight);

        // Ресурсы СЛЕВА от бара
        RenderTaskResources(canvas, task, x, y);

        // Имя задачи справа от бара
        RenderTaskName(canvas, task, x, y, width, barHeight);
        
        // Заметка справа от имени — возвращает ширину текста имени
        RenderTaskNote(canvas, task, x, y, width, barHeight);
    }

    private void RenderRegularTask(Canvas canvas, Task task, double x, double y, double width, double height)
    {
        // Определяем цвет бара: зелёный для 100%, обычный для остальных
        var isCompleted = task.Complete >= 1.0f;
        var barBrush = isCompleted ? _taskCompletedBrush : _taskBarBrush;
        var progressBrush = isCompleted ? _taskCompletedBrush : _taskBarProgressBrush;

        // Основной бар
        var taskBar = new Rectangle
        {
            Width = width,
            Height = height,
            Fill = barBrush,
            Stroke = isCompleted 
                ? new SolidColorBrush(Color.FromRgb(56, 142, 60))  // Darker green border
                : _taskBarBorderBrush,
            StrokeThickness = 1,
            RadiusX = 3,
            RadiusY = 3
        };

        Canvas.SetLeft(taskBar, x);
        Canvas.SetTop(taskBar, y);
        canvas.Children.Add(taskBar);

        // Прогресс (только если НЕ 100%)
        if (task.Complete > 0 && task.Complete < 1.0f)
        {
            var progressWidth = width * task.Complete;

            var progressBar = new Rectangle
            {
                Width = Math.Max(progressWidth, 2),
                Height = height,
                Fill = progressBrush,
                RadiusX = 3,
                RadiusY = 3,
                Clip = new RectangleGeometry(new Rect(0, 0, progressWidth, height), 3, 3)
            };

            Canvas.SetLeft(progressBar, x);
            Canvas.SetTop(progressBar, y);
            canvas.Children.Add(progressBar);
        }

        // Иконка галочки для 100% задач
        if (isCompleted)
        {
            var checkMark = new TextBlock
            {
                Text = "✓",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };

            checkMark.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            
            Canvas.SetLeft(checkMark, x + (width - checkMark.DesiredSize.Width) / 2);
            Canvas.SetTop(checkMark, y + (height - checkMark.DesiredSize.Height) / 2);
            canvas.Children.Add(checkMark);
        }
        // Процент выполнения (внутри бара, если достаточно места и не 100%)
        else if (width >= 40 && task.Complete > 0)
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
        // Вычисляем прогресс групповой задачи на основе дочерних задач
        float groupProgress = CalculateGroupProgress(task);
        bool isCompleted = groupProgress >= 1.0f;

        // Определяем цвет группы: зелёный для 100%, обычный для остальных
        var groupBrush = isCompleted ? _taskCompletedBrush : _groupTaskBrush;
        var progressBrush = isCompleted ? _taskCompletedBrush : _taskBarProgressBrush;

        var groupHeight = height * 0.4;
        var bracketHeight = height * 0.3;

        // Основная линия (фон)
        var mainLine = new Rectangle
        {
            Width = width,
            Height = groupHeight,
            Fill = groupBrush,
            RadiusX = 3,
            RadiusY = 3
        };

        Canvas.SetLeft(mainLine, x);
        Canvas.SetTop(mainLine, y + (height - groupHeight) / 2);
        canvas.Children.Add(mainLine);

        // Полоса прогресса (только если не 100%)
        if (groupProgress > 0 && groupProgress < 1.0f)
        {
            var progressWidth = width * groupProgress;

            var progressLine = new Rectangle
            {
                Width = Math.Max(progressWidth, 2),
                Height = groupHeight,
                Fill = progressBrush,
                RadiusX = 3,
                RadiusY = 3,
                Clip = new RectangleGeometry(new Rect(0, 0, progressWidth, groupHeight), 3, 3)
            };

            Canvas.SetLeft(progressLine, x);
            Canvas.SetTop(progressLine, y + (height - groupHeight) / 2);
            canvas.Children.Add(progressLine);
        }

        // Левая скобка (уголок вниз)
        var leftBracket = new Polygon
        {
            Points = new PointCollection
            {
                new Point(0, 0),
                new Point(8, 0),
                new Point(0, bracketHeight)
            },
            Fill = groupBrush
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
            Fill = groupBrush
        };

        Canvas.SetLeft(rightBracket, x + width - 8);
        Canvas.SetTop(rightBracket, y + (height - groupHeight) / 2 + groupHeight);
        canvas.Children.Add(rightBracket);

        // Иконка сворачивания (±)
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

        // Галочка для завершённых групп
        if (isCompleted)
        {
            var checkMark = new TextBlock
            {
                Text = "✓",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };

            checkMark.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(checkMark, x + (width - checkMark.DesiredSize.Width) / 2);
            Canvas.SetTop(checkMark, y + (height - checkMark.DesiredSize.Height) / 2);
            canvas.Children.Add(checkMark);
        }
        // Процент выполнения для групп (если достаточно места)
        else if (width >= 40 && groupProgress > 0)
        {
            var percentText = new TextBlock
            {
                Text = $"{(int)(groupProgress * 100)}%",
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

    /// <summary>
    /// Вычисляет прогресс групповой задачи на основе дочерних задач.
    /// </summary>
    private float CalculateGroupProgress(Task groupTask)
    {
        if (_control.ProjectManager == null)
            return 0f;
        
        var children = _control.ProjectManager.MembersOf(groupTask).ToList();
        if (children.Count == 0)
            return 0f;

        // Вычисляем средний прогресс по всем дочерним задачам
        float totalProgress = 0f;
        int count = 0;

        foreach (var child in children)
        {
            // Рекурсивно вычисляем прогресс для вложенных групп
            if (_control.ProjectManager.IsGroup(child))
            {
                totalProgress += CalculateGroupProgress(child);
            }
            else
            {
                totalProgress += child.Complete;
            }
            count++;
        }

        return count > 0 ? totalProgress / count : 0f;
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
    
    /// <summary>
    /// Отрисовывает инициалы назначенных ресурсов слева от бара.
    /// </summary>
    private void RenderTaskResources(Canvas canvas, Task task, double x, double y)
    {
        if (_resourceService == null) return;

        var initials = _resourceService.GetInitialsForTask(task.Id, ", ");
        if (string.IsNullOrEmpty(initials)) return;

        // Получаем ресурсы для определения цвета (берём цвет первого ресурса)
        var resources = _resourceService.GetResourcesForTask(task.Id).ToList();
        
        // Определяем цвет текста
        Brush textBrush = _control.TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray;

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

        // Создаём TextBlock с выравниванием по правому краю
        var resourceText = new TextBlock
        {
            Text = initials,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = textBrush,
            TextAlignment = TextAlignment.Right,
            Width = ResourceAreaWidth
        };

        // Позиционируем: правый край области ресурсов = левый край бара - отступ
        var resourceX = x - ResourceGap - ResourceAreaWidth;
        var resourceY = y + (_control.BarHeight - 14) / 2; // Центрируем по вертикали

        // Если выходит за левую границу — корректируем
        if (resourceX < 0)
        {
            resourceX = 2;
            resourceText.Width = Math.Max(x - ResourceGap - 2, 0);
            
            // Если места совсем нет - не показываем
            if (resourceText.Width < 20) return;
        }

        Canvas.SetLeft(resourceText, resourceX);
        Canvas.SetTop(resourceText, resourceY);

        canvas.Children.Add(resourceText);
    }

    /// <summary>
    /// Отрисовывает название задачи справа от бара.
    /// </summary>
    private void RenderTaskName(Canvas canvas, Task task, double x, double y, double width, double height)
    {
        // Определяем, является ли задача группой
        var isGroup = _control.ProjectManager?.IsGroup(task) ?? false;
        
        // Имя задачи справа от бара
        var name = task.Name ?? "Без названия";
    
        if (name.Length > 30)
            name = name.Substring(0, 27) + "...";
        
        var nameText = new TextBlock
        {
            Text = name,
            FontSize = isGroup ? 14 : 11, 
            FontWeight = isGroup ? FontWeights.Bold : FontWeights.DemiBold,
            Foreground = _control.TryFindResource("TextPrimaryBrush") as Brush ?? Brushes.Black
        };

        nameText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var textX = x + width + 8;
        var textY = y + (height - nameText.DesiredSize.Height) / 2;

        Canvas.SetLeft(nameText, textX);
        Canvas.SetTop(nameText, textY);
        canvas.Children.Add(nameText);
    }
    
    /// <summary>
    /// Отрисовывает красную "стену" deadline.
    /// </summary>
    private void RenderDeadline(Canvas canvas, Task task, double y, double barHeight)
    {
        if (!task.Deadline.HasValue) return;

        var deadlineX = task.Deadline.Value.Days * _control.ColumnWidth;
        
        // Толщина "стены"
        const double wallWidth = 3;
        
        // Высота стены = высота бара (НЕ выходит за строку)
        var wallHeight = barHeight;
        var wallY = y;

        // Основная стена
        var wall = new Rectangle
        {
            Width = wallWidth,
            Height = wallHeight,
            Fill = _deadlineBrush,
            RadiusX = 1,
            RadiusY = 1
        };

        Canvas.SetLeft(wall, deadlineX - wallWidth / 2);
        Canvas.SetTop(wall, wallY);
        canvas.Children.Add(wall);

        // Маленький флажок (в пределах или чуть выше бара)
        const double flagWidth = 6;
        const double flagHeight = 4;

        var flag = new Polygon
        {
            Points = new PointCollection
            {
                new Point(0, 0),
                new Point(flagWidth, 0),
                new Point(flagWidth, flagHeight * 0.7),
                new Point(flagWidth / 2, flagHeight),
                new Point(0, flagHeight * 0.7)
            },
            Fill = _deadlineBrush
        };

        // Флажок чуть выше бара, но в пределах barSpacing
        Canvas.SetLeft(flag, deadlineX - flagWidth / 2);
        Canvas.SetTop(flag, wallY - flagHeight);
        canvas.Children.Add(flag);
    }

    /// <summary>
    /// Отрисовывает заметку справа от названия задачи.
    /// </summary>
    /// <summary>
    /// Отрисовывает заметку справа от названия задачи (свёрнутая версия).
    /// </summary>
    private void RenderTaskNote(Canvas canvas, Task task, double x, double y, double width, double barHeight)
    {
        if (string.IsNullOrWhiteSpace(task.Note)) return;

        // Позиция после имени задачи
        var nameWidth = EstimateTextWidth(task.Name ?? "Без названия", 11);
        var noteX = x + width + 8 + nameWidth + 50;
        var noteY = y + (barHeight - 16) / 2;

        // Контейнер для заметки (кликабельная область)
        var noteContainer = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(40, 255, 200, 100)), // Полупрозрачный жёлтый
            BorderBrush = new SolidColorBrush(Color.FromRgb(200, 180, 100)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 2, 4, 2),
            Cursor = Cursors.Hand,
            ToolTip = "Нажмите для редактирования"
        };

        // Контент: иконка + текст
        var contentPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal
        };

        // Иконка заметки
        var noteIcon = new TextBlock
        {
            Text = "📝",
            FontSize = 10,
            Margin = new Thickness(0, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        contentPanel.Children.Add(noteIcon);

        // Текст заметки (обрезанный)
        var noteText = task.Note
            .Replace("\r\n", " ")
            .Replace("\n", " ")
            .Replace("\r", " ")
            .Trim();

        if (noteText.Length > 30)
        {
            noteText = noteText.Substring(0, 27) + "...";
        }

        var noteTextBlock = new TextBlock
        {
            Text = noteText,
            FontSize = 10,
            FontStyle = FontStyles.Italic,
            Foreground = new SolidColorBrush(Color.FromRgb(100, 80, 40)),
            MaxWidth = 150,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        contentPanel.Children.Add(noteTextBlock);

        noteContainer.Child = contentPanel;

        Canvas.SetLeft(noteContainer, noteX);
        Canvas.SetTop(noteContainer, noteY);
        canvas.Children.Add(noteContainer);
    }
    
    /// <summary>
    /// Оценивает ширину текста в пикселях.
    /// </summary>
    private double EstimateTextWidth(string text, double fontSize)
    {
        return Math.Min(text.Length * fontSize * 0.55, 200);
    }
}