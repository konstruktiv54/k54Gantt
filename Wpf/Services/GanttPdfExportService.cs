// ═══════════════════════════════════════════════════════════════════════════════
//                    ЭКСПОРТ ДИАГРАММЫ ГАНТА В PDF (v6 - FIXED)
// ═══════════════════════════════════════════════════════════════════════════════
//
// Изменения v6:
// - ИСПРАВЛЕНО: CreateSinglePagePdf теперь правильно вычисляет высоту страницы
// - ИСПРАВЛЕНО: Margin включён в pageHeight (верхний + нижний)
// - ИСПРАВЛЕНО: Используются РЕАЛЬНЫЕ размеры bitmap (ActualHeight/Width)
// - ИСПРАВЛЕНО: Футер рисуется от currentY, а не от pageHeight
// - ИСПРАВЛЕНО: Убраны магические числа, заменены на константы
// - ДОБАВЛЕНО: Диагностика для проверки корректности размещения
//
// Требуемый NuGet пакет: PdfSharpCore
// dotnet add package PdfSharpCore
//
// ═══════════════════════════════════════════════════════════════════════════════

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace Wpf.Services;

/// <summary>
/// Настройки экспорта в PDF.
/// </summary>
public class PdfExportSettings
{
    public string ProjectName { get; set; } = "Диаграмма Ганта";
    public DateTime ExportDate { get; set; } = DateTime.Now;
    public bool ShowHeader { get; set; } = true;
    public bool ShowPageNumbers { get; set; } = true;
    public bool ShowExportDate { get; set; } = true;
    public double Dpi { get; set; } = 150;
    public double MarginMm { get; set; } = 15;
    
    /// <summary>
    /// Режим размещения: true = вся диаграмма на одной странице (кастомный размер).
    /// </summary>
    public bool FitToSinglePage { get; set; } = true;
}

/// <summary>
/// Сервис для экспорта диаграммы Ганта в PDF.
/// </summary>
public class GanttPdfExportService
{
    private const double MmToPoint = 2.834645669;
    
    /// <summary>
    /// Экспортирует диаграмму Ганта в указанный файл.
    /// </summary>
    /// <remarks>
    /// КРИТИЧЕСКИ ВАЖНО: Использует РЕАЛЬНЫЕ размеры Canvas (ActualWidth/ActualHeight),
    /// а не теоретические вычисленные размеры, чтобы избежать обрезки.
    /// </remarks>
    public void ExportToPdfFile(
        Canvas chartCanvas, 
        Canvas headerCanvas,
        Canvas gridCanvas,
        double fullWidth,      // Теоретическая полная ширина (может быть > ActualWidth)
        double fullHeight,     // Теоретическая полная высота (может быть > ActualHeight)
        string filePath,
        Action? invalidateCallback,
        PdfExportSettings settings)
    {
        // Сохраняем оригинальные размеры
        var originalChartWidth = chartCanvas.Width;
        var originalChartHeight = chartCanvas.Height;
        var originalHeaderWidth = headerCanvas.Width;
        var originalHeaderHeight = headerCanvas.Height;
        var originalChartClip = chartCanvas.ClipToBounds;
        var originalHeaderClip = headerCanvas.ClipToBounds;

        try
        {
            var actualHeight = fullHeight*settings.Dpi/100;
            var actualWidth = fullWidth*settings.Dpi/100;
            var actualHeaderHeight = headerCanvas.ActualHeight*settings.Dpi/100;
            
            // ФАЗА 1: Устанавливаем полные размеры и перерисовываем
            chartCanvas.Width = actualWidth;
            chartCanvas.Height = actualHeight;
            headerCanvas.Width = actualWidth;
            chartCanvas.ClipToBounds = false;
            headerCanvas.ClipToBounds = false;

            // Перерисовываем
            invalidateCallback?.Invoke();

            //chartCanvas.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            chartCanvas.Measure(new Size(fullWidth, fullHeight));
            chartCanvas.Arrange(new Rect(0, 0, fullWidth, fullHeight));
            
            //headerCanvas.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            headerCanvas.Measure(new Size(fullWidth, headerCanvas.ActualHeight));
            headerCanvas.Arrange(new Rect(0, 0, fullWidth, headerCanvas.ActualHeight));
            
            chartCanvas.UpdateLayout();
            headerCanvas.UpdateLayout();

            System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                System.Windows.Threading.DispatcherPriority.Render, 
                new Action(() => { }));
            

            // ФАЗА 2: Рендерим в bitmap используя РЕАЛЬНЫЕ размеры

            var chartBitmap = RenderCanvasToBitmap(chartCanvas, settings.Dpi, actualWidth, actualHeight);
            //var gridBitmap = RenderCanvasToBitmap(headerCanvas, settings.Dpi, actualWidth, headerCanvas.Height);
            var headerBitmap = RenderCanvasToBitmap(headerCanvas, settings.Dpi, actualWidth, actualHeaderHeight);
            
            
            // var xpsPath1 = @"c:\Users\Konstruktiv54\Desktop\3.xps";
            // XpsService.SaveHeaderChartAndGridToXps_FixedPage(headerCanvas,chartCanvas,gridCanvas, xpsPath1, actualWidth,actualHeaderHeight,fullHeight);
            var xpsPath2 = @"c:\Users\Konstruktiv54\Desktop\4.xps";
            XpsService.SaveHeaderAndChartToXps_FixedPage(headerCanvas, chartCanvas, gridCanvas, xpsPath2);
            // var xpsPath2 = @"C:\temp\gantt1.xps";
            // XpsService.SaveCanvasToXps(headerCanvas, xpsPath2, fullWidth, fullHeight);
            
            // ФАЗА 4: Создаём PDF
            CreatePdfDocument(chartBitmap, headerBitmap, filePath, settings);
        }
        finally
        {
            // Восстанавливаем оригинальные размеры
            chartCanvas.Width = originalChartWidth;
            chartCanvas.Height = originalChartHeight;
            headerCanvas.Width = originalHeaderWidth;
            headerCanvas.Height = originalHeaderHeight;
            chartCanvas.ClipToBounds = originalChartClip;
            headerCanvas.ClipToBounds = originalHeaderClip;

            invalidateCallback?.Invoke();
            chartCanvas.UpdateLayout();
            headerCanvas.UpdateLayout();
        }
    }

    /// <summary>
    /// Создаёт PDF документ (выбирает режим в зависимости от настроек).
    /// </summary>
    private void CreatePdfDocument(BitmapSource chartBitmap, BitmapSource headerBitmap, 
        string filePath, PdfExportSettings settings)
    {
            CreateSinglePagePdf(chartBitmap, headerBitmap, filePath, settings);
    }

    /// <summary>
    /// Создаёт PDF с ОДНОЙ страницей, на которой помещается вся диаграмма.
    /// Размер страницы вычисляется на основе РЕАЛЬНЫХ размеров отрендеренных bitmap'ов.
    /// </summary>
    /// <remarks>
    /// Архитектура метода:
    /// 1. Вычисляем размеры bitmap'ов в точках PDF (на основе реальных PixelHeight/PixelWidth)
    /// 2. Последовательно размещаем элементы, отслеживая currentY
    /// 3. Вычисляем итоговую высоту страницы ПОСЛЕ размещения всех элементов
    /// 4. Создаём страницу с точным размером под весь контент
    /// </remarks>
    private void CreateSinglePagePdf(BitmapSource chartBitmap, BitmapSource headerBitmap,
        string filePath, PdfExportSettings settings)
    {
        var document = new PdfDocument();
        document.Info.Title = settings.ProjectName;
        document.Info.Author = Environment.UserName;
        document.Info.Subject = "Диаграмма Ганта";
        document.Info.Creator = "Gantt Chart Application";

        // ═══════════════════════════════════════════════════════════════════
        // Константы отступов (в точках PDF)
        // ═══════════════════════════════════════════════════════════════════
        const double HeaderToContentGap = 5.0;   // Отступ между текстовым заголовком и header диаграммы
        const double HeaderToChartGap = 5.0;     // Отступ между header диаграммы и chart
        const double ChartToFooterGap = 10.0;    // Отступ между chart и футером

        // ═══════════════════════════════════════════════════════════════════
        // Вычисляем РЕАЛЬНЫЕ размеры bitmap'ов в точках PDF
        // ═══════════════════════════════════════════════════════════════════
        var chartWidthPts = chartBitmap.PixelWidth * 72.0 / settings.Dpi;
        var chartHeightPts = chartBitmap.PixelHeight * 72.0 / settings.Dpi;
        var headerWidthPts = headerBitmap.PixelWidth * 72.0 / settings.Dpi;
        var headerHeightPts = headerBitmap.PixelHeight * 72.0 / settings.Dpi;

        // ═══════════════════════════════════════════════════════════════════
        // Поля страницы
        // ═══════════════════════════════════════════════════════════════════
        var margin = settings.MarginMm * MmToPoint;

        // ═══════════════════════════════════════════════════════════════════
        // Высоты секций (в точках PDF)
        // ═══════════════════════════════════════════════════════════════════
        var textHeaderHeight = settings.ShowHeader ? 40.0 : 0.0;
        var footerHeight = settings.ShowPageNumbers ? 20.0 : 0.0;

        // ═══════════════════════════════════════════════════════════════════
        // ВЫЧИСЛЯЕМ размер страницы на основе РЕАЛЬНЫХ размеров контента
        // ═══════════════════════════════════════════════════════════════════
        var contentWidth = Math.Max(chartWidthPts, headerWidthPts);
        var pageWidth = contentWidth + 2 * margin;

        var pageHeight = margin
                       + textHeaderHeight
                       + (textHeaderHeight > 0 ? HeaderToContentGap : 0)
                       + headerHeightPts
                       + HeaderToChartGap
                       + chartHeightPts
                       + ChartToFooterGap
                       + footerHeight
                       + margin;

        // ═══════════════════════════════════════════════════════════════════
        // Создаём страницу с вычисленным размером
        // ═══════════════════════════════════════════════════════════════════
        var page = document.AddPage();
        page.Width = pageWidth;
        page.Height = pageHeight;

        using var gfx = XGraphics.FromPdfPage(page);

        var currentY = margin;

        // ───────────────────────────────────────────────────────────────────
        // 1. Текстовый заголовок документа
        // ───────────────────────────────────────────────────────────────────
        if (settings.ShowHeader)
        {
            DrawPageHeader(gfx, settings, margin, currentY, contentWidth, 1, 1);
            currentY += textHeaderHeight + HeaderToContentGap;
        }

        // ───────────────────────────────────────────────────────────────────
        // 2. Header диаграммы
        // ───────────────────────────────────────────────────────────────────
        DrawFullBitmap(gfx, headerBitmap, margin, currentY, headerWidthPts, headerHeightPts);
        currentY += headerHeightPts + HeaderToChartGap;

        // ───────────────────────────────────────────────────────────────────
        // 3. Сама диаграмма
        // ───────────────────────────────────────────────────────────────────
        DrawFullBitmap(gfx, chartBitmap, margin, currentY, chartWidthPts, chartHeightPts);
        currentY += chartHeightPts + ChartToFooterGap;

        // ───────────────────────────────────────────────────────────────────
        // 4. Футер
        // ───────────────────────────────────────────────────────────────────
        if (settings.ShowPageNumbers)
        {
            DrawPageFooter(gfx, "Страница 1 из 1", margin, currentY, contentWidth);
            currentY += footerHeight;
        }

        // ═══════════════════════════════════════════════════════════════════
        // Проверка корректности размещения
        // ═══════════════════════════════════════════════════════════════════
        var expectedEndY = pageHeight - margin;
        var actualEndY = currentY;
        var difference = Math.Abs(expectedEndY - actualEndY);

        if (difference > 1.0)
        {
            System.Diagnostics.Debug.WriteLine($"⚠️ WARNING: Page height mismatch!");
            System.Diagnostics.Debug.WriteLine($"   Expected end Y: {expectedEndY:F2} pts");
            System.Diagnostics.Debug.WriteLine($"   Actual end Y:   {actualEndY:F2} pts");
            System.Diagnostics.Debug.WriteLine($"   Difference:     {difference:F2} pts");
        }

        document.Save(filePath);

        System.Diagnostics.Debug.WriteLine($"═══ PDF Export Summary ═══");
        System.Diagnostics.Debug.WriteLine($"Chart bitmap: {chartBitmap.PixelWidth} × {chartBitmap.PixelHeight} px");
        System.Diagnostics.Debug.WriteLine($"Header bitmap: {headerBitmap.PixelWidth} × {headerBitmap.PixelHeight} px");
        System.Diagnostics.Debug.WriteLine($"DPI: {settings.Dpi}");
        System.Diagnostics.Debug.WriteLine($"Chart in PDF: {chartWidthPts:F2} × {chartHeightPts:F2} pts");
        System.Diagnostics.Debug.WriteLine($"Page size: {pageWidth:F2} × {pageHeight:F2} pts");
        System.Diagnostics.Debug.WriteLine($"═════════════════════════");
    }

    /// <summary>
    /// Рисует весь bitmap целиком.
    /// </summary>
    private void DrawFullBitmap(XGraphics gfx, BitmapSource bitmap,
        double destX, double destY, double destWidth, double destHeight)
    {
        using var stream = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(stream);
        stream.Position = 0;

        var xImage = XImage.FromStream(() => new MemoryStream(stream.ToArray()));
        gfx.DrawImage(xImage, destX, destY, destWidth, destHeight);
    }
    
    private BitmapSource RenderCanvasToBitmap(Canvas canvas, double dpi, double targetWidth, double targetHeight)
    {
        // ФИКСИРУЕМ РАЗМЕРЫ - используем ТОЛЬКО переданные значения
        var width = targetWidth;
        var height = targetHeight;

        // Для диагностики
        System.Diagnostics.Debug.WriteLine($"RenderCanvasToBitmap:");
        System.Diagnostics.Debug.WriteLine($"  Target: {width:F0} × {height:F0}");
        System.Diagnostics.Debug.WriteLine($"  DPI: {dpi}");
        
        
        // ПРЯМОЕ ВЫЧИСЛЕНИЕ РАЗМЕРОВ В ПИКСЕЛЯХ (без сложной логики)
        var scale =  dpi / 96.0;
        var pixelWidth = (int)(width * scale);
        var pixelHeight = (int)(height * scale);
        
        // ПРОСТОЕ ОГРАНИЧЕНИЕ РАЗМЕРА (сохраняем пропорции)
        const int maxPixels = 32000;

        if (pixelWidth > maxPixels || pixelHeight > maxPixels)
        {
            var ratio = Math.Min((double)maxPixels / pixelWidth, (double)maxPixels / pixelHeight);
            pixelWidth = (int)(pixelWidth * ratio);
            pixelHeight = (int)(pixelHeight * ratio);
            scale *= ratio;

            System.Diagnostics.Debug.WriteLine($"  Scaled to: {pixelWidth} × {pixelHeight} (ratio: {ratio:F3})");
        }

        System.Diagnostics.Debug.WriteLine($"  Final pixels: {pixelWidth} × {pixelHeight}");
        System.Diagnostics.Debug.WriteLine($"  Final scale: {scale:F3}");
        
        // УПРОЩЕННЫЙ РЕНДЕРИНГ (убираем лишние преобразования)
        var renderBitmap = new RenderTargetBitmap(
            pixelWidth,
            pixelHeight,
            dpi,
            dpi,
            PixelFormats.Pbgra32);
        
        var drawingVisual = new DrawingVisual();

        using (var drawingContext = drawingVisual.RenderOpen())
        {
            // 1. Белый фон на ВСЮ площадь bitmap
            //drawingContext.DrawRectangle(Brushes.White, null, new Rect(0, 0, pixelWidth, pixelHeight));

            // 2. Прямой рендеринг Canvas с масштабированием
            // Убираем VisualBrush - используем прямое рисование
            drawingContext.PushTransform(new ScaleTransform(scale, scale));
            drawingContext.DrawRectangle(
                new VisualBrush(canvas)
                {
                    Stretch = Stretch.None,
                    Viewbox = new Rect(0, 0, width, height),
                    ViewboxUnits = BrushMappingMode.Absolute
                },
                null,
                new Rect(0, 0, width, height));
            drawingContext.Pop();
        }
        
        renderBitmap.Render(drawingVisual);
        SaveBitmapToPng(renderBitmap, "c:\\Users\\Konstruktiv54\\Desktop\\1.png");
        return renderBitmap;
    }
    
    // Сохраняет BitmapSource в PNG по указанному пути
    public static void SaveBitmapToPng(BitmapSource bitmap, string filePath)
    {
        if (bitmap == null) throw new ArgumentNullException(nameof(bitmap));
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            encoder.Save(fs);
        }
    }

    // Возвращает PNG как массив байтов (удобно для тестов)
    public static byte[] BitmapToPngBytes(BitmapSource bitmap)
    {
        if (bitmap == null) throw new ArgumentNullException(nameof(bitmap));
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var ms = new MemoryStream())
        {
            encoder.Save(ms);
            return ms.ToArray();
        }
    }

    private void DrawPageHeader(XGraphics gfx, PdfExportSettings settings,
        double x, double y, double width, int pageNum, int totalPages)
    {
        var titleFont = new XFont("Arial", 14, XFontStyle.Bold);
        var subtitleFont = new XFont("Arial", 9, XFontStyle.Regular);

        gfx.DrawString(settings.ProjectName, titleFont, XBrushes.Black, new XPoint(x, y + 15));

        if (settings.ShowExportDate)
        {
            var dateText = $"Экспорт: {settings.ExportDate:dd.MM.yyyy HH:mm}";
            var dateWidth = gfx.MeasureString(dateText, subtitleFont).Width;
            gfx.DrawString(dateText, subtitleFont, XBrushes.Gray,
                new XPoint(x + width - dateWidth, y + 15));
        }

        gfx.DrawLine(new XPen(XColors.LightGray, 0.5), x, y + 25, x + width, y + 25);
    }

    private void DrawPageFooter(XGraphics gfx, string text, double x, double y, double width)
    {
        var font = new XFont("Arial", 9, XFontStyle.Regular);
        var textWidth = gfx.MeasureString(text, font).Width;
        gfx.DrawString(text, font, XBrushes.Gray, new XPoint(x + (width - textWidth) / 2, y));
    }

    private void DrawBitmapSection(XGraphics gfx, BitmapSource source, Int32Rect sourceRect,
        double destX, double destY, double destWidth, double destHeight)
    {
        if (sourceRect.X < 0) sourceRect.X = 0;
        if (sourceRect.Y < 0) sourceRect.Y = 0;
        if (sourceRect.X >= source.PixelWidth) return;
        if (sourceRect.Y >= source.PixelHeight) return;
        if (sourceRect.X + sourceRect.Width > source.PixelWidth)
            sourceRect.Width = source.PixelWidth - sourceRect.X;
        if (sourceRect.Y + sourceRect.Height > source.PixelHeight)
            sourceRect.Height = source.PixelHeight - sourceRect.Y;
        if (sourceRect.Width <= 0 || sourceRect.Height <= 0) return;

        var croppedBitmap = new CroppedBitmap(source, sourceRect);

        using var stream = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(croppedBitmap));
        encoder.Save(stream);
        stream.Position = 0;

        var xImage = XImage.FromStream(() => new MemoryStream(stream.ToArray()));
        gfx.DrawImage(xImage, destX, destY, destWidth, destHeight);
    }
}