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

using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace Wpf.Services;

/// <summary>
/// Формат бумаги.
/// </summary>
public enum PaperFormat
{
    A4,
    A3,
    A2,
    A1,
    Letter,
    Legal,
    Tabloid
}

/// <summary>
/// Ориентация страницы.
/// </summary>
public enum PageOrientation
{
    Portrait,
    Landscape
}

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
    public double Scale { get; set; } = 1.0;
    public double Dpi { get; set; } = 150;
    public double MarginMm { get; set; } = 15;
    public PaperFormat PaperFormat { get; set; } = PaperFormat.A4;
    public PageOrientation Orientation { get; set; } = PageOrientation.Landscape;
    
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

    private static (double Width, double Height) GetPageSize(PaperFormat format)
    {
        return format switch
        {
            PaperFormat.A4 => (595.0, 842.0),
            PaperFormat.A3 => (842.0, 1191.0),
            PaperFormat.A2 => (1191.0, 1684.0),
            PaperFormat.A1 => (1684.0, 2384.0),
            PaperFormat.Letter => (612.0, 792.0),
            PaperFormat.Legal => (612.0, 1008.0),
            PaperFormat.Tabloid => (792.0, 1224.0),
            _ => (595.0, 842.0)
        };
    }

    /// <summary>
    /// Экспортирует диаграмму Ганта в PDF.
    /// </summary>
    public bool ExportToPdf(
        Canvas chartCanvas, 
        Canvas headerCanvas,
        double fullWidth, 
        double fullHeight,
        Action? invalidateCallback = null,
        PdfExportSettings? settings = null)
    {
        settings ??= new PdfExportSettings();

        var saveDialog = new SaveFileDialog
        {
            Filter = "PDF документ (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            FileName = $"{settings.ProjectName}_{DateTime.Now:yyyy-MM-dd}"
        };

        if (saveDialog.ShowDialog() != true)
            return false;

        try
        {
            ExportToPdfFile(chartCanvas, headerCanvas, fullWidth, fullHeight, 
                saveDialog.FileName, invalidateCallback, settings);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = saveDialog.FileName,
                UseShellExecute = true
            });

            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Ошибка при экспорте в PDF:\n{ex.Message}",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
    }

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
            // ═══════════════════════════════════════════════════════════════════
            // ФАЗА 1: Устанавливаем полные размеры и перерисовываем
            // ═══════════════════════════════════════════════════════════════════
            chartCanvas.Width = fullWidth;
            chartCanvas.Height = fullHeight;
            headerCanvas.Width = fullWidth;
            chartCanvas.ClipToBounds = false;
            headerCanvas.ClipToBounds = false;

            // Перерисовываем
            invalidateCallback?.Invoke();
            
            chartCanvas.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            chartCanvas.Arrange(new Rect(0, 0, fullWidth, fullHeight));
            
            headerCanvas.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            headerCanvas.Arrange(new Rect(0, 0, fullWidth, headerCanvas.DesiredSize.Height));
            
            chartCanvas.UpdateLayout();
            headerCanvas.UpdateLayout();

            System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                System.Windows.Threading.DispatcherPriority.Render, 
                new Action(() => { }));

            // ═══════════════════════════════════════════════════════════════════
            // ФАЗА 2: Получаем РЕАЛЬНЫЕ размеры после рендеринга
            // ═══════════════════════════════════════════════════════════════════
            var actualChartWidth = chartCanvas.ActualWidth > 0 ? chartCanvas.ActualWidth : fullWidth;
            var actualChartHeight = chartCanvas.ActualHeight > 0 ? chartCanvas.ActualHeight : fullHeight;
            var actualHeaderWidth = headerCanvas.ActualWidth > 0 ? headerCanvas.ActualWidth : fullWidth;
            var actualHeaderHeight = headerCanvas.ActualHeight > 0 ? headerCanvas.ActualHeight : 50;

            System.Diagnostics.Debug.WriteLine($"═══ Export Size Analysis ═══");
            System.Diagnostics.Debug.WriteLine($"Requested:  Chart {fullWidth:F0} × {fullHeight:F0}");
            System.Diagnostics.Debug.WriteLine($"Actual:     Chart {actualChartWidth:F0} × {actualChartHeight:F0}");
            System.Diagnostics.Debug.WriteLine($"Difference: {Math.Abs(fullHeight - actualChartHeight):F0} units");
            System.Diagnostics.Debug.WriteLine($"═══════════════════════════");

            // ═══════════════════════════════════════════════════════════════════
            // ФАЗА 3: Рендерим в bitmap используя РЕАЛЬНЫЕ размеры
            // ═══════════════════════════════════════════════════════════════════
            var chartBitmap = RenderCanvasToBitmap(chartCanvas, settings.Dpi, actualChartWidth, actualChartHeight);
            var headerBitmap = RenderCanvasToBitmap(headerCanvas, settings.Dpi, actualHeaderWidth, actualHeaderHeight);

            // ═══════════════════════════════════════════════════════════════════
            // ФАЗА 4: Создаём PDF
            // ═══════════════════════════════════════════════════════════════════
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
        if (settings.FitToSinglePage)
        {
            CreateSinglePagePdf(chartBitmap, headerBitmap, filePath, settings);
        }
        else
        {
            CreateMultiPagePdf(chartBitmap, headerBitmap, filePath, settings);
        }
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

    /// <summary>
    /// Создаёт PDF с несколькими страницами (существующая логика).
    /// </summary>
    private void CreateMultiPagePdf(BitmapSource chartBitmap, BitmapSource headerBitmap,
        string filePath, PdfExportSettings settings)
    {
        var document = new PdfDocument();
        document.Info.Title = settings.ProjectName;
        document.Info.Author = Environment.UserName;
        document.Info.Subject = "Диаграмма Ганта";
        document.Info.Creator = "Gantt Chart Application";

        var (baseWidth, baseHeight) = GetPageSize(settings.PaperFormat);
        var pageWidth = settings.Orientation == PageOrientation.Landscape ? baseHeight : baseWidth;
        var pageHeight = settings.Orientation == PageOrientation.Landscape ? baseWidth : baseHeight;

        var margin = settings.MarginMm * MmToPoint;
        var contentWidth = pageWidth - 2 * margin;
        var contentHeight = pageHeight - 2 * margin;
        var headerAreaHeight = settings.ShowHeader ? 40.0 : 0;
        var footerAreaHeight = settings.ShowPageNumbers ? 20.0 : 0;
        var imageAreaHeight = contentHeight - headerAreaHeight - footerAreaHeight - 10;

        var chartWidthPts = chartBitmap.PixelWidth * 72.0 / settings.Dpi;
        var chartHeightPts = chartBitmap.PixelHeight * 72.0 / settings.Dpi;
        var headerHeightPts = headerBitmap.PixelHeight * 72.0 / settings.Dpi;

        var baseScaleFactor = contentWidth / chartWidthPts;
        var effectiveScale = baseScaleFactor * settings.Scale;

        var scaledChartWidth = chartWidthPts * effectiveScale;
        var scaledChartHeight = chartHeightPts * effectiveScale;
        var scaledHeaderHeight = Math.Min(headerHeightPts * effectiveScale, 80.0);

        var columnsNeeded = Math.Max(1, (int)Math.Ceiling(scaledChartWidth / contentWidth));
        var chartWidthPerPage = scaledChartWidth / columnsNeeded;
        var availableHeightPerPage = imageAreaHeight - scaledHeaderHeight;
        var rowsNeeded = Math.Max(1, (int)Math.Ceiling(scaledChartHeight / availableHeightPerPage));
        var totalPages = columnsNeeded * rowsNeeded;

        for (var pageNum = 1; pageNum <= totalPages; pageNum++)
        {
            var col = (pageNum - 1) % columnsNeeded;
            var row = (pageNum - 1) / columnsNeeded;

            var page = document.AddPage();
            page.Width = pageWidth;
            page.Height = pageHeight;

            using var gfx = XGraphics.FromPdfPage(page);

            if (settings.ShowHeader)
            {
                DrawPageHeader(gfx, settings, margin, margin, contentWidth, pageNum, totalPages);
            }

            var imageStartY = margin + headerAreaHeight;

            if (row == 0)
            {
                var headerSourceX = (int)(col * headerBitmap.PixelWidth / columnsNeeded);
                var headerSourceWidth = Math.Min(
                    (int)(headerBitmap.PixelWidth / columnsNeeded),
                    headerBitmap.PixelWidth - headerSourceX);

                if (headerSourceWidth > 0)
                {
                    var headerSourceRect = new Int32Rect(headerSourceX, 0, headerSourceWidth, headerBitmap.PixelHeight);
                    DrawBitmapSection(gfx, headerBitmap, headerSourceRect,
                        margin, imageStartY, chartWidthPerPage, scaledHeaderHeight);
                }

                imageStartY += scaledHeaderHeight;
            }

            var sourceX = (int)(col * chartBitmap.PixelWidth / columnsNeeded);
            var sourceY = (int)(row * availableHeightPerPage / effectiveScale * settings.Dpi / 72.0);
            var sourceWidth = Math.Min(
                (int)(chartBitmap.PixelWidth / columnsNeeded),
                chartBitmap.PixelWidth - sourceX);
            var sourceHeight = Math.Min(
                (int)(availableHeightPerPage / effectiveScale * settings.Dpi / 72.0),
                chartBitmap.PixelHeight - sourceY);

            if (sourceWidth > 0 && sourceHeight > 0 && sourceY < chartBitmap.PixelHeight)
            {
                var chartSourceRect = new Int32Rect(sourceX, sourceY, sourceWidth, sourceHeight);
                var destHeight = sourceHeight * 72.0 / settings.Dpi * effectiveScale;

                DrawBitmapSection(gfx, chartBitmap, chartSourceRect,
                    margin, imageStartY, chartWidthPerPage, destHeight);
            }

            if (settings.ShowPageNumbers)
            {
                var pageText = columnsNeeded > 1
                    ? $"Страница {pageNum} из {totalPages} (колонка {col + 1}, ряд {row + 1})"
                    : $"Страница {pageNum} из {totalPages}";
                DrawPageFooter(gfx, pageText, margin, pageHeight - margin, contentWidth);
            }
        }

        document.Save(filePath);
    }

    private BitmapSource RenderCanvasToBitmap(Canvas canvas, double dpi, double targetWidth, double targetHeight)
    {
        var width = targetWidth > 0 ? targetWidth : (canvas.ActualWidth > 0 ? canvas.ActualWidth : 800);
        var height = targetHeight > 0 ? targetHeight : (canvas.ActualHeight > 0 ? canvas.ActualHeight : 600);

        var scale = dpi / 96.0;
        var pixelWidth = (int)(width * scale);
        var pixelHeight = (int)(height * scale);

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