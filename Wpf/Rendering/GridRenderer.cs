// GanttChart.WPF/Rendering/GridRenderer.cs
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Wpf.Controls;

namespace Wpf.Rendering;

/// <summary>
/// Рендерер сетки диаграммы Ганта.
/// Отвечает за отрисовку вертикальных линий и выделение выходных дней.
/// </summary>
public class GridRenderer
{
    private readonly GanttChartControl _control;

    // Кэшированные кисти
    private Brush? _gridLineBrush;
    private Brush? _weekendBrush;

    public GridRenderer(GanttChartControl control)
    {
        _control = control;
    }

    /// <summary>
    /// Рендерит сетку на указанном Canvas.
    /// </summary>
    public void Render(Canvas canvas)
    {
        if (_control.ProjectManager == null)
            return;

        // Получаем кисти из ресурсов
        _gridLineBrush ??= _control.FindResource("GridLineBrush") as Brush ?? Brushes.LightGray;
        _weekendBrush ??= _control.FindResource("WeekendBackgroundBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(249, 249, 249));

        var width = canvas.Width;
        var height = canvas.Height;
        var columnWidth = _control.ColumnWidth;
        var projectStart = _control.ProjectManager.Start;

        var totalDays = (int)(width / columnWidth) + 1;

        // Рисуем выходные дни (фон)
        if (_control.HighlightWeekends)
        {
            RenderWeekends(canvas, projectStart, totalDays, height);
        }

        // Рисуем вертикальные линии сетки
        RenderVerticalLines(canvas, totalDays, height);

        // Рисуем горизонтальные линии (между рядами задач)
        RenderHorizontalLines(canvas, width);
    }

    private void RenderWeekends(Canvas canvas, DateTime projectStart, int totalDays, double height)
    {
        for (int day = 0; day < totalDays; day++)
        {
            var date = projectStart.AddDays(day);
            
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            {
                var rect = new Rectangle
                {
                    Width = _control.ColumnWidth,
                    Height = height,
                    Fill = _weekendBrush
                };

                Canvas.SetLeft(rect, day * _control.ColumnWidth);
                Canvas.SetTop(rect, 0);

                canvas.Children.Add(rect);
            }
        }
    }

    private void RenderVerticalLines(Canvas canvas, int totalDays, double height)
    {
        var projectStart = _control.ProjectManager!.Start;

        for (int day = 0; day <= totalDays; day++)
        {
            var x = day * _control.ColumnWidth;
            var date = projectStart.AddDays(day);

            // Более толстая линия для начала месяца
            var strokeThickness = date.Day == 1 ? 1.5 : 0.5;
            var opacity = date.Day == 1 ? 1.0 : 0.5;

            var line = new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = height,
                Stroke = _gridLineBrush,
                StrokeThickness = strokeThickness,
                Opacity = opacity
            };

            canvas.Children.Add(line);
        }
    }

    private void RenderHorizontalLines(Canvas canvas, double width)
    {
        if (_control.ProjectManager == null)
            return;

        var taskCount = _control.ProjectManager.Tasks.Count;
        var rowHeight = _control.RowHeight;

        for (int row = 0; row <= taskCount; row++)
        {
            var y = row * rowHeight;

            var line = new Line
            {
                X1 = 0,
                Y1 = y,
                X2 = width,
                Y2 = y,
                Stroke = _gridLineBrush,
                StrokeThickness = 0.5,
                Opacity = 0.3
            };

            canvas.Children.Add(line);
        }
    }
}