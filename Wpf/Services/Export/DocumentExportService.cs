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

        // Создаём FixedPage с полными размерами документа
        var fixedPage = new FixedPage
        {
            Width = data.TotalWidth,
            Height = data.TotalHeight,
            Background = Brushes.White
        };

        double currentY = 0;

        // ═══════════════════════════════════════════════════════════════════
        // 1. HEADER (заголовок с месяцами и днями)
        // ═══════════════════════════════════════════════════════════════════
        var headerClone = CloneCanvasAsVisual(
            data.GanttChart.Header.Canvas,
            data.GanttChart.Header.Width,
            data.GanttChart.Header.Height);
        
        FixedPage.SetLeft(headerClone, 0);
        FixedPage.SetTop(headerClone, currentY);
        fixedPage.Children.Add(headerClone);
        
        currentY += data.GanttChart.Header.Height;

        // ═══════════════════════════════════════════════════════════════════
        // 2. GRID LAYER (сетка, выходные дни)
        // ═══════════════════════════════════════════════════════════════════
        var gridClone = CloneCanvasAsVisual(
            data.GanttChart.Grid.Canvas,
            data.GanttChart.Grid.Width,
            data.GanttChart.Grid.Height);
        
        FixedPage.SetLeft(gridClone, 0);
        FixedPage.SetTop(gridClone, currentY);
        fixedPage.Children.Add(gridClone);

        // ═══════════════════════════════════════════════════════════════════
        // 3. TASK LAYER (бары задач) - поверх сетки
        // ═══════════════════════════════════════════════════════════════════
        var taskClone = CloneCanvasAsVisual(
            data.GanttChart.Tasks.Canvas,
            data.GanttChart.Tasks.Width,
            data.GanttChart.Tasks.Height);
        
        FixedPage.SetLeft(taskClone, 0);
        FixedPage.SetTop(taskClone, currentY);
        fixedPage.Children.Add(taskClone);

        // ═══════════════════════════════════════════════════════════════════
        // 4. TODAY LINE (линия "Сегодня") - поверх задач
        // ═══════════════════════════════════════════════════════════════════
        var todayClone = CloneCanvasAsVisual(
            data.GanttChart.TodayLine.Canvas,
            data.GanttChart.TodayLine.Width,
            data.GanttChart.TodayLine.Height);
        
        FixedPage.SetLeft(todayClone, 0);
        FixedPage.SetTop(todayClone, currentY);
        fixedPage.Children.Add(todayClone);

        currentY += Math.Max(data.GanttChart.Grid.Height, data.GanttChart.Tasks.Height);

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

            // Canvas загрузки ресурсов
            var engagementClone = CloneCanvasAsVisual(
                data.EngagementStrip.Engagement.Canvas,
                data.EngagementStrip.Engagement.Width,
                data.EngagementStrip.Engagement.Height);
            
            FixedPage.SetLeft(engagementClone, 0);
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

        // Используем VisualBrush для захвата содержимого
        var visualBrush = new VisualBrush(source)
        {
            Stretch = Stretch.None,
            AlignmentX = AlignmentX.Left,
            AlignmentY = AlignmentY.Top,
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, source.ActualWidth, source.ActualHeight)
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