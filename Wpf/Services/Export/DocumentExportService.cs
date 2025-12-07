using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Xps;
using System.Windows.Xps.Packaging;

namespace Wpf.Services.Export;

/// <summary>
/// Сервис экспорта документа в XPS.
/// Координирует сборку слоёв из разных компонентов.
/// </summary>
public static class DocumentExportService
{
    /// <summary>
    /// Экспортирует документ в XPS файл.
    /// </summary>
    /// <param name="data">Данные документа для экспорта.</param>
    /// <param name="filePath">Путь к файлу XPS.</param>
    public static void ExportToXps(DocumentExportData data, string filePath)
    {
        if (data.GanttChart == null)
            throw new ArgumentException("GanttChart data is required", nameof(data));

        // Удаляем существующий файл (XpsDocument не перезаписывает)
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        // Смещение timeline (ширина Sidebar или ResourceNames)
        double timelineOffsetX = data.TimelineOffsetX;

        // Создаём FixedPage с полными размерами документа
        var fixedPage = new FixedPage
        {
            Width = data.TotalWidth,
            Height = data.TotalHeight,
            Background = Brushes.White
        };

        double currentY = 0;

        // ═══════════════════════════════════════════════════════════════════
        // 1. SIDEBAR HEADER (заголовки колонок таблицы)
        // ═══════════════════════════════════════════════════════════════════
        if (data.Sidebar != null)
        {
            var sidebarHeaderClone = CloneCanvasAsVisual(
                data.Sidebar.Header.Canvas,
                data.Sidebar.Header.Width,
                data.Sidebar.Header.Height);

            FixedPage.SetLeft(sidebarHeaderClone, 0);
            FixedPage.SetTop(sidebarHeaderClone, currentY);
            fixedPage.Children.Add(sidebarHeaderClone);
        }

        // ═══════════════════════════════════════════════════════════════════
        // 1.1. TIMELINE HEADER (заголовок с месяцами и днями)
        // ═══════════════════════════════════════════════════════════════════
        var headerClone = CloneCanvasAsVisual(
            data.GanttChart.Header.Canvas,
            data.GanttChart.Header.Width,
            data.GanttChart.Header.Height);

        FixedPage.SetLeft(headerClone, timelineOffsetX);
        FixedPage.SetTop(headerClone, currentY);
        fixedPage.Children.Add(headerClone);

        currentY += data.GanttChart.Header.Height;

        // ═══════════════════════════════════════════════════════════════════
        // 2. SIDEBAR ROWS (строки задач)
        // ═══════════════════════════════════════════════════════════════════
        if (data.Sidebar != null)
        {
            var sidebarRowsClone = CloneCanvasAsVisual(
                data.Sidebar.Rows.Canvas,
                data.Sidebar.Rows.Width,
                data.Sidebar.Rows.Height);

            FixedPage.SetLeft(sidebarRowsClone, 0);
            FixedPage.SetTop(sidebarRowsClone, currentY);
            fixedPage.Children.Add(sidebarRowsClone);
        }

        // ═══════════════════════════════════════════════════════════════════
        // 2.1. GRID LAYER (сетка, выходные дни)
        // ═══════════════════════════════════════════════════════════════════
        var gridClone = CloneCanvasAsVisual(
            data.GanttChart.Grid.Canvas,
            data.GanttChart.Grid.Width,
            data.GanttChart.Grid.Height);

        FixedPage.SetLeft(gridClone, timelineOffsetX);
        FixedPage.SetTop(gridClone, currentY);
        fixedPage.Children.Add(gridClone);

        // ═══════════════════════════════════════════════════════════════════
        // 3. TASK LAYER (бары задач) - поверх сетки
        // ═══════════════════════════════════════════════════════════════════
        var taskClone = CloneCanvasAsVisual(
            data.GanttChart.Tasks.Canvas,
            data.GanttChart.Tasks.Width,
            data.GanttChart.Tasks.Height);

        FixedPage.SetLeft(taskClone, timelineOffsetX);
        FixedPage.SetTop(taskClone, currentY);
        fixedPage.Children.Add(taskClone);

        // ═══════════════════════════════════════════════════════════════════
        // 4. TODAY LINE (линия "Сегодня") - поверх задач
        // ═══════════════════════════════════════════════════════════════════
        var todayClone = CloneCanvasAsVisual(
            data.GanttChart.TodayLine.Canvas,
            data.GanttChart.TodayLine.Width,
            data.GanttChart.TodayLine.Height);

        FixedPage.SetLeft(todayClone, timelineOffsetX);
        FixedPage.SetTop(todayClone, currentY);
        fixedPage.Children.Add(todayClone);

        currentY += Math.Max(data.GanttChart.Grid.Height, data.GanttChart.Tasks.Height);

        // ═══════════════════════════════════════════════════════════════════
        // 4.5. ДУБЛИРУЮЩИЕ ЗАГОЛОВКИ (над EngagementStrip)
        // ═══════════════════════════════════════════════════════════════════
        if (data.EngagementStrip != null)
        {
            // Sidebar Header (дубль)
            if (data.Sidebar != null)
            {
                var sidebarHeaderClone2 = CloneCanvasAsVisual(
                    data.Sidebar.Header.Canvas,
                    data.Sidebar.Header.Width,
                    data.Sidebar.Header.Height);

                FixedPage.SetLeft(sidebarHeaderClone2, 0);
                FixedPage.SetTop(sidebarHeaderClone2, currentY);
                fixedPage.Children.Add(sidebarHeaderClone2);
            }

            // Timeline Header (дубль)
            var headerClone2 = CloneCanvasAsVisual(
                data.GanttChart.Header.Canvas,
                data.GanttChart.Header.Width,
                data.GanttChart.Header.Height);

            FixedPage.SetLeft(headerClone2, timelineOffsetX);
            FixedPage.SetTop(headerClone2, currentY);
            fixedPage.Children.Add(headerClone2);

            currentY += data.GanttChart.Header.Height;
        }

        // ═══════════════════════════════════════════════════════════════════
        // 5. ENGAGEMENT STRIP (если есть)
        // ═══════════════════════════════════════════════════════════════════
        if (data.EngagementStrip != null)
        {
            // Разделительная линия
            currentY += data.SectionGap / 2;

            var separator = CreateSeparatorLine(data.TotalWidth);
            FixedPage.SetLeft(separator, 0);
            FixedPage.SetTop(separator, currentY);
            fixedPage.Children.Add(separator);

            currentY += data.SectionGap / 2;

            // Колонка с именами ресурсов (выравнивание по правому краю)
            if (data.EngagementStrip.ResourceNames != null)
            {
                var namesClone = CloneCanvasAsVisual(
                    data.EngagementStrip.ResourceNames.Canvas,
                    data.EngagementStrip.ResourceNames.Width,
                    data.EngagementStrip.ResourceNames.Height);

                // Выравнивание по правому краю области timelineOffsetX
                var namesX = timelineOffsetX - data.EngagementStrip.ResourceNames.Width;

                FixedPage.SetLeft(namesClone, namesX);
                FixedPage.SetTop(namesClone, currentY);
                fixedPage.Children.Add(namesClone);
            }

            // Canvas загрузки ресурсов
            var engagementClone = CloneCanvasAsVisual(
                data.EngagementStrip.Engagement.Canvas,
                data.EngagementStrip.Engagement.Width,
                data.EngagementStrip.Engagement.Height);

            FixedPage.SetLeft(engagementClone, timelineOffsetX);
            FixedPage.SetTop(engagementClone, currentY);
            fixedPage.Children.Add(engagementClone);
        }

        // ═══════════════════════════════════════════════════════════════════
        // 6. СОХРАНЕНИЕ В XPS
        // ═══════════════════════════════════════════════════════════════════
        var fixedDocument = new FixedDocument();
        var pageContent = new PageContent();
        ((IAddChild)pageContent).AddChild(fixedPage);
        fixedDocument.Pages.Add(pageContent);

        using var xpsDoc = new XpsDocument(filePath, FileAccess.Write);
        var writer = XpsDocument.CreateXpsDocumentWriter(xpsDoc);
        writer.Write(fixedDocument);
    }

    /// <summary>
    /// Клонирует Canvas как визуальный элемент для экспорта.
    /// </summary>
    private static Canvas CloneCanvasAsVisual(Canvas source, double width, double height)
    {
        var canvasCopy = new Canvas
        {
            Width = width,
            Height = height,
            Background = Brushes.Transparent
        };

        // Для динамически созданных Canvas нужно вызвать Measure/Arrange
        // чтобы ActualWidth/ActualHeight были корректны
        if (source.ActualWidth <= 0 || source.ActualHeight <= 0)
        {
            source.Measure(new Size(width, height));
            source.Arrange(new Rect(0, 0, width, height));
        }

        // Используем реальные размеры или заданные, если Actual* всё ещё 0
        var sourceWidth = source.ActualWidth > 0 ? source.ActualWidth : width;
        var sourceHeight = source.ActualHeight > 0 ? source.ActualHeight : height;

        // Используем VisualBrush для захвата содержимого
        var visualBrush = new VisualBrush(source)
        {
            Stretch = Stretch.None,
            AlignmentX = AlignmentX.Left,
            AlignmentY = AlignmentY.Top,
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, sourceWidth, sourceHeight)
        };

        var rect = new Rectangle
        {
            Width = width,
            Height = height,
            Fill = visualBrush
        };

        canvasCopy.Children.Add(rect);
        return canvasCopy;
    }

    /// <summary>
    /// Создаёт разделительную линию между секциями.
    /// </summary>
    private static Line CreateSeparatorLine(double width)
    {
        return new Line
        {
            X1 = 0,
            Y1 = 0,
            X2 = width,
            Y2 = 0,
            Stroke = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 2 }
        };
    }
}