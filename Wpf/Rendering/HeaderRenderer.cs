// GanttChart.WPF/Rendering/HeaderRenderer.cs
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Wpf.Controls;

namespace Wpf.Rendering;

/// <summary>
/// Рендерер заголовка диаграммы Ганта.
/// Отображает две строки: месяцы и дни.
/// </summary>
public class HeaderRenderer
{
    private readonly GanttChartControl _control;

    // Кэшированные ресурсы
    private Brush? _headerBackgroundBrush;
    private Brush? _gridLineBrush;
    private Brush? _textPrimaryBrush;
    private Brush? _textSecondaryBrush;
    private Typeface? _typeface;

    public HeaderRenderer(GanttChartControl control)
    {
        _control = control;
    }

    /// <summary>
    /// Рендерит заголовок на указанном Canvas.
    /// </summary>
    public void Render(Canvas canvas)
    {
        if (_control.ProjectManager == null)
            return;

        // Инициализация ресурсов
        InitializeResources();

        var width = canvas.Width;
        var headerHeight = _control.HeaderHeight;
        var columnWidth = _control.ColumnWidth;
        var projectStart = _control.ProjectManager.Start;

        var totalDays = (int)(width / columnWidth) + 1;

        // Высота строки месяцев и строки дней
        var monthRowHeight = headerHeight * 0.5;
        var dayRowHeight = headerHeight * 0.5;

        // Рисуем строку месяцев
        RenderMonthRow(canvas, projectStart, totalDays, monthRowHeight);

        // Рисуем строку дней
        RenderDayRow(canvas, projectStart, totalDays, monthRowHeight, dayRowHeight);

        // Разделительная линия между месяцами и днями
        var separatorLine = new Line
        {
            X1 = 0,
            Y1 = monthRowHeight,
            X2 = width,
            Y2 = monthRowHeight,
            Stroke = _gridLineBrush,
            StrokeThickness = 1
        };
        canvas.Children.Add(separatorLine);
    }

    private void InitializeResources()
    {
        _headerBackgroundBrush ??= _control.FindResource("HeaderBackgroundBrush") as Brush ?? Brushes.LightGray;
        _gridLineBrush ??= _control.FindResource("GridLineBrush") as Brush ?? Brushes.Gray;
        _textPrimaryBrush ??= _control.FindResource("TextPrimaryBrush") as Brush ?? Brushes.Black;
        _textSecondaryBrush ??= _control.FindResource("TextSecondaryBrush") as Brush ?? Brushes.DarkGray;
        _typeface ??= new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    }

    private void RenderMonthRow(Canvas canvas, DateTime projectStart, int totalDays, double rowHeight)
    {
        var columnWidth = _control.ColumnWidth;
        var currentMonth = projectStart.Month;
        var currentYear = projectStart.Year;
        var monthStartDay = 0;

        for (int day = 0; day <= totalDays; day++)
        {
            var date = projectStart.AddDays(day);

            // Если месяц изменился или это последний день, рисуем предыдущий месяц
            if (date.Month != currentMonth || day == totalDays)
            {
                var monthWidth = (day - monthStartDay) * columnWidth;
                var monthX = monthStartDay * columnWidth;

                // Фон месяца (чередование для визуального разделения)
                var monthIndex = (currentYear * 12 + currentMonth);
                var monthBackground = monthIndex % 2 == 0 
                    ? _headerBackgroundBrush 
                    : new SolidColorBrush(Color.FromRgb(232, 232, 232));

                var monthRect = new Rectangle
                {
                    Width = monthWidth,
                    Height = rowHeight,
                    Fill = monthBackground
                };
                Canvas.SetLeft(monthRect, monthX);
                Canvas.SetTop(monthRect, 0);
                canvas.Children.Add(monthRect);

                // Название месяца
                var monthName = new DateTime(currentYear, currentMonth, 1).ToString("MMMM yyyy", CultureInfo.CurrentCulture);
                
                var monthText = new TextBlock
                {
                    Text = monthName,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = _textPrimaryBrush,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                // Центрируем текст
                monthText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var textX = monthX + (monthWidth - monthText.DesiredSize.Width) / 2;
                var textY = (rowHeight - monthText.DesiredSize.Height) / 2;

                Canvas.SetLeft(monthText, Math.Max(monthX + 4, textX));
                Canvas.SetTop(monthText, textY);
                canvas.Children.Add(monthText);

                // Вертикальная линия разделителя месяцев
                if (day < totalDays)
                {
                    var monthSeparator = new Line
                    {
                        X1 = day * columnWidth,
                        Y1 = 0,
                        X2 = day * columnWidth,
                        Y2 = rowHeight,
                        Stroke = _gridLineBrush,
                        StrokeThickness = 1
                    };
                    canvas.Children.Add(monthSeparator);
                }

                // Обновляем для следующего месяца
                currentMonth = date.Month;
                currentYear = date.Year;
                monthStartDay = day;
            }
        }
    }

    private void RenderDayRow(Canvas canvas, DateTime projectStart, int totalDays, double offsetY, double rowHeight)
    {
        var columnWidth = _control.ColumnWidth;

        for (int day = 0; day < totalDays; day++)
        {
            var date = projectStart.AddDays(day);
            var x = day * columnWidth;

            // Фон для выходных
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            {
                var weekendRect = new Rectangle
                {
                    Width = columnWidth,
                    Height = rowHeight,
                    Fill = new SolidColorBrush(Color.FromRgb(245, 245, 245))
                };
                Canvas.SetLeft(weekendRect, x);
                Canvas.SetTop(weekendRect, offsetY);
                canvas.Children.Add(weekendRect);
            }

            // Номер дня (показываем только если достаточно места)
            if (columnWidth >= 15)
            {
                var dayText = new TextBlock
                {
                    Text = date.Day.ToString(),
                    FontSize = columnWidth >= 25 ? 10 : 8,
                    Foreground = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday
                        ? _textSecondaryBrush
                        : _textPrimaryBrush,
                    TextAlignment = TextAlignment.Center
                };

                dayText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var textX = x + (columnWidth - dayText.DesiredSize.Width) / 2;
                var textY = offsetY + (rowHeight - dayText.DesiredSize.Height) / 2;

                Canvas.SetLeft(dayText, textX);
                Canvas.SetTop(dayText, textY);
                canvas.Children.Add(dayText);
            }

            // Вертикальная линия
            var dayLine = new Line
            {
                X1 = x,
                Y1 = offsetY,
                X2 = x,
                Y2 = offsetY + rowHeight,
                Stroke = _gridLineBrush,
                StrokeThickness = 0.5,
                Opacity = 0.5
            };
            canvas.Children.Add(dayLine);
        }
    }
}