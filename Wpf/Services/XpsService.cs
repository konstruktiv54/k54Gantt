using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Xps.Packaging;
using System.Windows.Shapes;
using System.Windows.Markup;
using System.Windows.Xps;
using Path = System.IO.Path;

namespace Wpf.Services;

public static class XpsService
{
    /// <summary>
    /// Сохраняет Header и Chart Canvas в XPS вектором.
    /// </summary>
    public static void SaveHeaderAndChartToXps_FixedPage(
        Canvas headerCanvas,
        Canvas chartCanvas,
        Canvas gridCanvas,
        Canvas todayCanvas,
        double chartStart,// можно передать null, если не нужен GridRenderer
        double chartWidth,
        string xpsPath)
    {
        // 1. Определяем ОБЩУЮ ширину как максимальную из всех Canvas
        double commonWidth = Math.Max(
            headerCanvas.ActualWidth,
            Math.Max(
                chartCanvas.ActualWidth, 
                gridCanvas?.ActualWidth ?? 0
            )
        );
        // 2. Используем реальные высоты каждого Canvas
        double headerHeight = headerCanvas.ActualHeight;
        double chartHeight = chartCanvas.ActualHeight;
        double gridHeight = gridCanvas?.ActualHeight ?? 0;

        // 3. Общая высота страницы
        double pageHeight = headerHeight + Math.Max(chartHeight, gridHeight);

        // 4. Создаём FixedPage с ОБЩИМИ размерами
        FixedPage fixedPage = new FixedPage
        {
            Width = commonWidth,
            Height = pageHeight,
            Background = Brushes.White
        };
        
        // 5. Header - сверху, растягиваем на всю ширину
        var headerClone = CloneCanvasAsVisual(headerCanvas, commonWidth, headerHeight);
        FixedPage.SetLeft(headerClone, 0);
        FixedPage.SetTop(headerClone, 0);
        fixedPage.Children.Add(headerClone);

        // 6. GridCanvas - под header, ТА ЖЕ ШИРИНА
        if (gridCanvas != null)
        {
            var gridClone = CloneCanvasAsVisual(gridCanvas, commonWidth, gridHeight);
            FixedPage.SetLeft(gridClone, 0);
            FixedPage.SetTop(gridClone, headerHeight);
            fixedPage.Children.Add(gridClone);
        }
    
        // 7. ChartCanvas - под header, ТА ЖЕ ШИРИНА
        var chartClone = CloneCanvasAsVisual(chartCanvas, commonWidth, chartHeight);
        FixedPage.SetLeft(chartClone, 0);
        FixedPage.SetTop(chartClone, headerHeight);
        fixedPage.Children.Add(chartClone);
        
        // 7. ChartCanvas - под header, ТА ЖЕ ШИРИНА
        var todayClone= CloneCanvasAsVisual(todayCanvas, commonWidth, gridHeight);
        FixedPage.SetLeft(todayClone, 0);
        FixedPage.SetTop(todayClone, headerHeight);
        fixedPage.Children.Add(todayClone);
    
        // 8. Создаём FixedDocument
        FixedDocument fixedDocument = new FixedDocument();
        PageContent pageContent = new PageContent();
        ((IAddChild)pageContent).AddChild(fixedPage);
        fixedDocument.Pages.Add(pageContent);

        // 9. Сохраняем в XPS
        using (var xpsDoc = new XpsDocument(xpsPath, FileAccess.Write))
        {
            XpsDocumentWriter writer = XpsDocument.CreateXpsDocumentWriter(xpsDoc);
            writer.Write(fixedDocument);
        }
    }
        
    private static Canvas CloneCanvasAsVisual(Canvas source, double width, double height)
    {
        var canvasCopy = new Canvas
        {
            Width = width,
            Height = height,
            Background = Brushes.Transparent
        };

        // Создаем VisualBrush с НАСТРОЙКАМИ ДЛЯ ВЫРАВНИВАНИЯ
        var visualBrush = new VisualBrush(source)
        {
            Stretch = Stretch.None, // Не растягиваем - сохраняем оригинальные пропорции
            AlignmentX = AlignmentX.Left, // Выравнивание по левому краю
            AlignmentY = AlignmentY.Top,  // Выравнивание по верхнему краю
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
}