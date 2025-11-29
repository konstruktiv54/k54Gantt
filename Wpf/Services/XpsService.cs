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
        double chartStart,// можно передать null, если не нужен GridRenderer
        double chartWidth,
        string xpsPath)
    {
        // 1. Рассчитываем размеры страницы
        double pageWidth = Math.Max(
            headerCanvas.ActualWidth,
            chartCanvas.ActualWidth
        );
        double pageHeight = headerCanvas.ActualHeight + chartCanvas.ActualHeight;

        // 2. Создаём FixedPage
        FixedPage fixedPage = new FixedPage
        {
            Width = pageWidth,
            Height = pageHeight,
            Background = Brushes.White
        };

        // 3. Размещаем headerCanvas сверху
        var headerClone = CloneCanvasAsVisual(headerCanvas);
        FixedPage.SetLeft(headerClone, 0);
        FixedPage.SetTop(headerClone, 0);
        fixedPage.Children.Add(headerClone);

        // 4. Добавляем gridCanvas, если есть (GridRenderer можно вызвать перед этим)
        var gridClone = CloneCanvasAsVisual(gridCanvas);
        FixedPage.SetLeft(gridClone, 0);
        FixedPage.SetTop(gridClone, headerCanvas.ActualHeight); // отступ сверху равен высоте header
        fixedPage.Children.Add(gridClone);
        
        // 5. Размещаем chartCanvas под headerCanvas
        var chartClone = CloneCanvasAsVisual(chartCanvas, chartWidth);
        FixedPage.SetLeft(chartClone, chartStart);
        FixedPage.SetTop(chartClone, headerCanvas.ActualHeight);
        fixedPage.Children.Add(chartClone);
        
        // 6. Создаём FixedDocument и добавляем страницу
        FixedDocument fixedDocument = new FixedDocument();
        PageContent pageContent = new PageContent();
        ((IAddChild)pageContent).AddChild(fixedPage);
        fixedDocument.Pages.Add(pageContent);

        // 7. Сохраняем в XPS
        using (var xpsDoc = new XpsDocument(xpsPath, FileAccess.Write))
        {
            XpsDocumentWriter writer = XpsDocument.CreateXpsDocumentWriter(xpsDoc);
            writer.Write(fixedDocument);
        }
    }
        
    private static Canvas CloneCanvasAsVisual(Canvas source, double redWidth = 0)
    {
        var width = redWidth == 0 ? source.ActualWidth : redWidth;
        
        var height = source.ActualHeight;

        var canvasCopy = new Canvas
        {
            Width = width,
            Height = height,
            Background = Brushes.Transparent
        };

        // Рисуем исходный Canvas в VisualBrush
        var rect = new Rectangle
        {
            Width = width,
            Height = height,
            Fill = new VisualBrush(source)
        };

        canvasCopy.Children.Add(rect);
        return canvasCopy;
    }
}
