// ═══════════════════════════════════════════════════════════════════════════════
//        ПЕЧАТЬ ДИАГРАММЫ ГАНТА (v2)
// ═══════════════════════════════════════════════════════════════════════════════
//
// Изменения v2:
// - Добавлен callback для перерисовки
// - Временное расширение Canvas перед рендерингом
//
// ═══════════════════════════════════════════════════════════════════════════════

using System;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Wpf.Services;

/// <summary>
/// Сервис печати диаграммы Ганта.
/// </summary>
public class GanttPrintService
{
    /// <summary>
    /// Печатает диаграмму через системный диалог.
    /// </summary>
    /// <param name="chartCanvas">Canvas с задачами</param>
    /// <param name="headerCanvas">Canvas с заголовком</param>
    /// <param name="fullWidth">Полная ширина рабочей области</param>
    /// <param name="fullHeight">Полная высота рабочей области</param>
    /// <param name="documentName">Название документа</param>
    /// <param name="invalidateCallback">Callback для перерисовки диаграммы</param>
    public bool Print(
        Canvas chartCanvas, 
        Canvas headerCanvas,
        double fullWidth, 
        double fullHeight, 
        string documentName,
        Action? invalidateCallback = null)
    {
        var printDialog = new PrintDialog();
        printDialog.PrintTicket.PageOrientation = System.Printing.PageOrientation.Landscape;

        if (printDialog.ShowDialog() != true)
            return false;

        try
        {
            // ═══════════════════════════════════════════════════════════════════
            // Временно расширяем Canvas и перерисовываем
            // ═══════════════════════════════════════════════════════════════════
            var originalChartWidth = chartCanvas.Width;
            var originalChartHeight = chartCanvas.Height;
            var originalHeaderWidth = headerCanvas.Width;
            var originalHeaderHeight = headerCanvas.Height;
            var originalChartClip = chartCanvas.ClipToBounds;
            var originalHeaderClip = headerCanvas.ClipToBounds;

            try
            {
                // Устанавливаем полные размеры
                chartCanvas.Width = fullWidth;
                chartCanvas.Height = fullHeight;
                headerCanvas.Width = fullWidth;

                // Отключаем clipping
                chartCanvas.ClipToBounds = false;
                headerCanvas.ClipToBounds = false;

                // Перерисовываем
                invalidateCallback?.Invoke();

                chartCanvas.UpdateLayout();
                headerCanvas.UpdateLayout();

                // Даём WPF время на обновление
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                    System.Windows.Threading.DispatcherPriority.Render,
                    new Action(() => { }));

                // Создаём документ для печати
                var document = CreatePrintDocument(chartCanvas, headerCanvas, 
                    fullWidth, fullHeight, printDialog, documentName);

                // Печатаем
                printDialog.PrintDocument(document.DocumentPaginator, documentName);
            }
            finally
            {
                // Восстанавливаем размеры
                chartCanvas.Width = originalChartWidth;
                chartCanvas.Height = originalChartHeight;
                headerCanvas.Width = originalHeaderWidth;
                headerCanvas.Height = originalHeaderHeight;
                chartCanvas.ClipToBounds = originalChartClip;
                headerCanvas.ClipToBounds = originalHeaderClip;

                // Перерисовываем обратно
                invalidateCallback?.Invoke();

                chartCanvas.UpdateLayout();
                headerCanvas.UpdateLayout();
            }

            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Ошибка при печати:\n{ex.Message}",
                "Ошибка печати",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
    }

    /// <summary>
    /// Создаёт документ для печати с разбиением на страницы.
    /// </summary>
    private FixedDocument CreatePrintDocument(
        Canvas chartCanvas, 
        Canvas headerCanvas,
        double fullWidth,
        double fullHeight,
        PrintDialog printDialog, 
        string documentName)
    {
        var document = new FixedDocument();

        var printableArea = printDialog.PrintableAreaWidth;
        var printableHeight = printDialog.PrintableAreaHeight;
        var margin = 40.0;

        var contentWidth = printableArea - 2 * margin;
        var contentHeight = printableHeight - 2 * margin;
        var headerHeight = 50.0;
        var footerHeight = 30.0;
        var chartHeaderHeight = 50.0;

        // Рендерим Canvas в изображения с ПОЛНЫМИ размерами
        var chartBitmap = RenderCanvasToBitmap(chartCanvas, 150, fullWidth, fullHeight);
        var headerBitmap = RenderCanvasToBitmap(headerCanvas, 150, fullWidth, 
            headerCanvas.ActualHeight > 0 ? headerCanvas.ActualHeight : 50);

        // Вычисляем масштаб и количество страниц
        var scaleFactor = contentWidth / chartBitmap.PixelWidth;
        var scaledChartHeight = chartBitmap.PixelHeight * scaleFactor;
        var scaledHeaderHeight = Math.Min(headerBitmap.PixelHeight * scaleFactor, chartHeaderHeight);

        var availableHeightPerPage = contentHeight - headerHeight - footerHeight - scaledHeaderHeight - 20;
        var totalPages = Math.Max(1, (int)Math.Ceiling(scaledChartHeight / availableHeightPerPage));

        for (var pageNum = 1; pageNum <= totalPages; pageNum++)
        {
            var page = CreatePage(
                chartBitmap,
                headerBitmap,
                printableArea,
                printableHeight,
                margin,
                contentWidth,
                scaleFactor,
                scaledHeaderHeight,
                availableHeightPerPage,
                pageNum,
                totalPages,
                documentName);

            document.Pages.Add(page);
        }

        return document;
    }

    /// <summary>
    /// Создаёт одну страницу документа.
    /// </summary>
    private PageContent CreatePage(
        BitmapSource chartBitmap,
        BitmapSource headerBitmap,
        double pageWidth,
        double pageHeight,
        double margin,
        double contentWidth,
        double scaleFactor,
        double scaledHeaderHeight,
        double availableHeightPerPage,
        int pageNum,
        int totalPages,
        string documentName)
    {
        var pageContent = new PageContent();
        var fixedPage = new FixedPage
        {
            Width = pageWidth,
            Height = pageHeight,
            Background = Brushes.White
        };

        var currentY = margin;

        // Заголовок страницы
        var titleText = new TextBlock
        {
            Text = documentName,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.Black
        };
        FixedPage.SetLeft(titleText, margin);
        FixedPage.SetTop(titleText, currentY);
        fixedPage.Children.Add(titleText);

        var dateText = new TextBlock
        {
            Text = $"Дата: {DateTime.Now:dd.MM.yyyy}",
            FontSize = 10,
            Foreground = Brushes.Gray
        };
        FixedPage.SetLeft(dateText, pageWidth - margin - 120);
        FixedPage.SetTop(dateText, currentY + 4);
        fixedPage.Children.Add(dateText);

        currentY += 30;

        // Линия под заголовком
        var headerLine = new System.Windows.Shapes.Line
        {
            X1 = 0,
            Y1 = 0,
            X2 = contentWidth,
            Y2 = 0,
            Stroke = Brushes.LightGray,
            StrokeThickness = 1
        };
        FixedPage.SetLeft(headerLine, margin);
        FixedPage.SetTop(headerLine, currentY);
        fixedPage.Children.Add(headerLine);

        currentY += 15;

        // Заголовок диаграммы (шапка с датами)
        var headerImage = new Image
        {
            Source = headerBitmap,
            Width = contentWidth,
            Height = scaledHeaderHeight,
            Stretch = Stretch.Fill
        };
        FixedPage.SetLeft(headerImage, margin);
        FixedPage.SetTop(headerImage, currentY);
        fixedPage.Children.Add(headerImage);

        currentY += scaledHeaderHeight + 5;

        // Часть диаграммы для этой страницы
        var sourceY = (int)((pageNum - 1) * availableHeightPerPage / scaleFactor);
        var sourceHeight = (int)Math.Min(
            availableHeightPerPage / scaleFactor,
            chartBitmap.PixelHeight - sourceY);

        if (sourceHeight > 0 && sourceY < chartBitmap.PixelHeight)
        {
            var croppedBitmap = new CroppedBitmap(
                chartBitmap,
                new Int32Rect(0, sourceY, chartBitmap.PixelWidth, sourceHeight));

            var chartImage = new Image
            {
                Source = croppedBitmap,
                Width = contentWidth,
                Height = sourceHeight * scaleFactor,
                Stretch = Stretch.Fill
            };

            FixedPage.SetLeft(chartImage, margin);
            FixedPage.SetTop(chartImage, currentY);
            fixedPage.Children.Add(chartImage);
        }

        // Нумерация страниц
        var pageNumberText = new TextBlock
        {
            Text = $"Страница {pageNum} из {totalPages}",
            FontSize = 10,
            Foreground = Brushes.Gray
        };
        pageNumberText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        FixedPage.SetLeft(pageNumberText, margin + (contentWidth - pageNumberText.DesiredSize.Width) / 2);
        FixedPage.SetTop(pageNumberText, pageHeight - margin);
        fixedPage.Children.Add(pageNumberText);

        ((IAddChild)pageContent).AddChild(fixedPage);
        return pageContent;
    }

    /// <summary>
    /// Рендерит Canvas в Bitmap с указанными размерами.
    /// </summary>
    private BitmapSource RenderCanvasToBitmap(Canvas canvas, double dpi, double targetWidth, double targetHeight)
    {
        var width = targetWidth > 0 ? targetWidth : (canvas.ActualWidth > 0 ? canvas.ActualWidth : 800);
        var height = targetHeight > 0 ? targetHeight : (canvas.ActualHeight > 0 ? canvas.ActualHeight : 600);

        var scale = dpi / 96.0;
        var pixelWidth = (int)(width * scale);
        var pixelHeight = (int)(height * scale);

        // Ограничение размера bitmap
        const int maxPixels = 16000;
        if (pixelWidth > maxPixels)
        {
            var ratio = (double)maxPixels / pixelWidth;
            pixelWidth = maxPixels;
            pixelHeight = (int)(pixelHeight * ratio);
            scale *= ratio;
        }
        if (pixelHeight > maxPixels)
        {
            var ratio = (double)maxPixels / pixelHeight;
            pixelHeight = maxPixels;
            pixelWidth = (int)(pixelWidth * ratio);
            scale *= ratio;
        }

        var renderBitmap = new RenderTargetBitmap(
            pixelWidth,
            pixelHeight,
            dpi,
            dpi,
            PixelFormats.Pbgra32);

        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            // Белый фон
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, pixelWidth, pixelHeight));

            var brush = new VisualBrush(canvas)
            {
                Stretch = Stretch.None,
                AlignmentX = AlignmentX.Left,
                AlignmentY = AlignmentY.Top,
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(0, 0, width, height),
                ViewportUnits = BrushMappingMode.Absolute,
                Viewport = new Rect(0, 0, width, height)
            };

            context.PushTransform(new ScaleTransform(scale, scale));
            context.DrawRectangle(brush, null, new Rect(0, 0, width, height));
            context.Pop();
        }

        renderBitmap.Render(visual);
        return renderBitmap;
    }
}