// ═══════════════════════════════════════════════════════════════════════════════
//        АЛЬТЕРНАТИВНЫЙ ВАРИАНТ: ПЕЧАТЬ БЕЗ ВНЕШНИХ БИБЛИОТЕК
// ═══════════════════════════════════════════════════════════════════════════════
//
// Этот вариант использует встроенные в WPF средства:
// 1. PrintDialog — стандартный диалог печати Windows
// 2. XPS — формат документов (можно конвертировать в PDF)
// 3. "Microsoft Print to PDF" — виртуальный принтер Windows 10+
//
// Преимущества:
// - Не требует внешних NuGet пакетов
// - Использует системный диалог печати
// - Поддерживает все принтеры (включая PDF)
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
/// Сервис печати диаграммы Ганта (без внешних библиотек).
/// </summary>
public class GanttPrintService
{
    /// <summary>
    /// Печатает диаграмму через системный диалог.
    /// Пользователь может выбрать "Microsoft Print to PDF" для создания PDF.
    /// </summary>
    public bool Print(Canvas chartCanvas, Canvas headerCanvas, string documentName = "Диаграмма Ганта")
    {
        var printDialog = new PrintDialog();
        
        // Устанавливаем альбомную ориентацию по умолчанию
        printDialog.PrintTicket.PageOrientation = (System.Printing.PageOrientation?)PageOrientation.Landscape;
        
        if (printDialog.ShowDialog() != true)
            return false;

        try
        {
            // Создаём документ для печати
            var document = CreatePrintDocument(chartCanvas, headerCanvas, printDialog, documentName);
            
            // Печатаем
            printDialog.PrintDocument(document.DocumentPaginator, documentName);
            
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
    /// Быстрая печать в PDF (использует "Microsoft Print to PDF").
    /// </summary>
    public bool QuickPrintToPdf(Canvas chartCanvas, Canvas headerCanvas, string documentName = "Диаграмма Ганта")
    {
        try
        {
            var printDialog = new PrintDialog();
            
            // Ищем принтер "Microsoft Print to PDF"
            var printServer = new LocalPrintServer();
            var pdfPrinter = printServer.GetPrintQueues()
                .FirstOrDefault(p => p.Name.Contains("PDF", StringComparison.OrdinalIgnoreCase));

            if (pdfPrinter != null)
            {
                printDialog.PrintQueue = pdfPrinter;
            }
            else
            {
                // Если нет PDF принтера — показываем диалог
                if (printDialog.ShowDialog() != true)
                    return false;
            }

            printDialog.PrintTicket.PageOrientation = (System.Printing.PageOrientation?)PageOrientation.Landscape;
            
            var document = CreatePrintDocument(chartCanvas, headerCanvas, printDialog, documentName);
            printDialog.PrintDocument(document.DocumentPaginator, documentName);
            
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Ошибка: {ex.Message}\n\nПопробуйте использовать обычную печать.",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }
    }

    /// <summary>
    /// Создаёт документ для печати с разбиением на страницы.
    /// </summary>
    private FixedDocument CreatePrintDocument(Canvas chartCanvas, Canvas headerCanvas, 
        PrintDialog printDialog, string documentName)
    {
        var document = new FixedDocument();
        
        // Размеры печатной области
        var printableArea = printDialog.PrintableAreaWidth;
        var printableHeight = printDialog.PrintableAreaHeight;
        var margin = 40.0;

        var contentWidth = printableArea - 2 * margin;
        var contentHeight = printableHeight - 2 * margin;
        var headerHeight = 50.0;
        var footerHeight = 30.0;
        var chartHeaderHeight = 50.0; // Высота заголовка диаграммы (даты)

        // Рендерим Canvas в изображения
        var chartBitmap = RenderCanvasToBitmap(chartCanvas, 150);
        var headerBitmap = RenderCanvasToBitmap(headerCanvas, 150);

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

        // ═══════════════════════════════════════════════════════════════════
        // Заголовок страницы
        // ═══════════════════════════════════════════════════════════════════
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

        // ═══════════════════════════════════════════════════════════════════
        // Заголовок диаграммы (шапка с датами)
        // ═══════════════════════════════════════════════════════════════════
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

        // ═══════════════════════════════════════════════════════════════════
        // Часть диаграммы для этой страницы
        // ═══════════════════════════════════════════════════════════════════
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

        // ═══════════════════════════════════════════════════════════════════
        // Нумерация страниц
        // ═══════════════════════════════════════════════════════════════════
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
    /// Рендерит Canvas в Bitmap.
    /// </summary>
    private BitmapSource RenderCanvasToBitmap(Canvas canvas, double dpi)
    {
        var width = canvas.ActualWidth > 0 ? canvas.ActualWidth : canvas.Width;
        var height = canvas.ActualHeight > 0 ? canvas.ActualHeight : canvas.Height;

        if (width <= 0 || height <= 0)
        {
            width = 800;
            height = 600;
        }

        var scale = dpi / 96.0;
        var pixelWidth = (int)(width * scale);
        var pixelHeight = (int)(height * scale);

        var renderBitmap = new RenderTargetBitmap(
            pixelWidth,
            pixelHeight,
            dpi,
            dpi,
            PixelFormats.Pbgra32);

        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            var brush = new VisualBrush(canvas)
            {
                Stretch = Stretch.None,
                AlignmentX = AlignmentX.Left,
                AlignmentY = AlignmentY.Top
            };

            context.PushTransform(new ScaleTransform(scale, scale));
            context.DrawRectangle(brush, null, new Rect(0, 0, width, height));
            context.Pop();
        }

        renderBitmap.Render(visual);
        return renderBitmap;
    }
}