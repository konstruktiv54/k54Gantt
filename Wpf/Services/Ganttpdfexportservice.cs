// ═══════════════════════════════════════════════════════════════════════════════
//                    ЭКСПОРТ ДИАГРАММЫ ГАНТА В PDF
// ═══════════════════════════════════════════════════════════════════════════════
//
// Требуемый NuGet пакет: PdfSharp (или PdfSharpCore для .NET 6+)
//
// Установка:
// dotnet add package PdfSharpCore
// или
// Install-Package PdfSharpCore
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
/// Настройки экспорта в PDF.
/// </summary>
public class PdfExportSettings
{
    /// <summary>
    /// Название проекта (отображается в заголовке).
    /// </summary>
    public string ProjectName { get; set; } = "Диаграмма Ганта";
    
    /// <summary>
    /// Дата экспорта.
    /// </summary>
    public DateTime ExportDate { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Показывать заголовок на каждой странице.
    /// </summary>
    public bool ShowHeader { get; set; } = true;
    
    /// <summary>
    /// Показывать нумерацию страниц.
    /// </summary>
    public bool ShowPageNumbers { get; set; } = true;
    
    /// <summary>
    /// Показывать дату экспорта.
    /// </summary>
    public bool ShowExportDate { get; set; } = true;
    
    /// <summary>
    /// Масштаб (1.0 = 100%, 0.5 = 50%).
    /// </summary>
    public double Scale { get; set; } = 1.0;
    
    /// <summary>
    /// DPI для рендеринга (96 = экран, 150-300 = печать).
    /// </summary>
    public double Dpi { get; set; } = 150;
    
    /// <summary>
    /// Отступы страницы (мм).
    /// </summary>
    public double MarginMm { get; set; } = 15;
    
    /// <summary>
    /// Ориентация страницы.
    /// </summary>
    public PageOrientation Orientation { get; set; } = PageOrientation.Landscape;
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
/// Сервис для экспорта диаграммы Ганта в PDF.
/// </summary>
public class GanttPdfExportService
{
    private const double MmToPoint = 2.834645669; // 1 мм = 2.834645669 точек PDF
    
    /// <summary>
    /// Экспортирует диаграмму Ганта в PDF с диалогом сохранения.
    /// </summary>
    public bool ExportToPdf(Canvas chartCanvas, Canvas headerCanvas, PdfExportSettings? settings = null)
    {
        settings ??= new PdfExportSettings();
        
        // Диалог сохранения
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
            ExportToPdfFile(chartCanvas, headerCanvas, saveDialog.FileName, settings);
            
            // Открыть PDF после создания
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
    public void ExportToPdfFile(Canvas chartCanvas, Canvas headerCanvas, string filePath, PdfExportSettings settings)
    {
        // Рендерим Canvas в изображение
        var chartBitmap = RenderCanvasToBitmap(chartCanvas, settings.Dpi, settings.Scale);
        var headerBitmap = RenderCanvasToBitmap(headerCanvas, settings.Dpi, settings.Scale);

        // Создаём PDF документ
        var document = new PdfDocument();
        document.Info.Title = settings.ProjectName;
        document.Info.Author = Environment.UserName;
        document.Info.Subject = "Диаграмма Ганта";
        document.Info.Creator = "Gantt Chart Application";

        // Размеры страницы
        var pageWidth = settings.Orientation == PageOrientation.Landscape ? 842.0 : 595.0;  // A4
        var pageHeight = settings.Orientation == PageOrientation.Landscape ? 595.0 : 842.0;
        var margin = settings.MarginMm * MmToPoint;
        
        var contentWidth = pageWidth - 2 * margin;
        var contentHeight = pageHeight - 2 * margin;
        var headerAreaHeight = settings.ShowHeader ? 40.0 : 0;
        var footerAreaHeight = settings.ShowPageNumbers ? 20.0 : 0;
        var imageAreaHeight = contentHeight - headerAreaHeight - footerAreaHeight - 10;

        // Высота заголовка диаграммы (масштабированная)
        var scaledHeaderHeight = headerBitmap.Height * contentWidth / chartBitmap.Width;
        if (scaledHeaderHeight > 60) scaledHeaderHeight = 60;
        
        // Вычисляем сколько страниц нужно
        var imageWidth = chartBitmap.Width;
        var imageHeight = chartBitmap.Height;
        var scaleFactor = contentWidth / imageWidth;
        var scaledImageHeight = imageHeight * scaleFactor;
        var availableHeightPerPage = imageAreaHeight - scaledHeaderHeight;
        var totalPages = (int)Math.Ceiling(scaledImageHeight / availableHeightPerPage);

        // Создаём страницы
        for (var pageNum = 1; pageNum <= totalPages; pageNum++)
        {
            var page = document.AddPage();
            page.Width = pageWidth;
            page.Height = pageHeight;

            using var gfx = XGraphics.FromPdfPage(page);
            
            // Заголовок страницы
            if (settings.ShowHeader)
            {
                DrawPageHeader(gfx, settings, margin, margin, contentWidth, pageNum, totalPages);
            }

            var imageStartY = margin + headerAreaHeight;

            // Заголовок диаграммы (шапка с датами)
            var headerSourceRect = new Int32Rect(0, 0, headerBitmap.PixelWidth, headerBitmap.PixelHeight);
            DrawBitmapSection(gfx, headerBitmap, headerSourceRect, 
                margin, imageStartY, contentWidth, scaledHeaderHeight);

            imageStartY += scaledHeaderHeight;

            // Часть диаграммы для этой страницы
            var sourceY = (int)((pageNum - 1) * availableHeightPerPage / scaleFactor);
            var sourceHeight = (int)Math.Min(availableHeightPerPage / scaleFactor, imageHeight - sourceY);
            
            if (sourceHeight > 0)
            {
                var chartSourceRect = new Int32Rect(0, sourceY, chartBitmap.PixelWidth, sourceHeight);
                var destHeight = sourceHeight * scaleFactor;
                
                DrawBitmapSection(gfx, chartBitmap, chartSourceRect,
                    margin, imageStartY, contentWidth, destHeight);
            }

            // Нумерация страниц
            if (settings.ShowPageNumbers)
            {
                DrawPageFooter(gfx, pageNum, totalPages, margin, pageHeight - margin, contentWidth);
            }
        }

        // Сохраняем
        document.Save(filePath);
    }

    /// <summary>
    /// Рендерит Canvas в Bitmap.
    /// </summary>
    private BitmapSource RenderCanvasToBitmap(Canvas canvas, double dpi, double scale)
    {
        var width = canvas.ActualWidth > 0 ? canvas.ActualWidth : canvas.Width;
        var height = canvas.ActualHeight > 0 ? canvas.ActualHeight : canvas.Height;

        if (width <= 0 || height <= 0)
        {
            width = 800;
            height = 600;
        }

        var scaledWidth = (int)(width * scale * dpi / 96.0);
        var scaledHeight = (int)(height * scale * dpi / 96.0);

        var renderBitmap = new RenderTargetBitmap(
            scaledWidth,
            scaledHeight,
            dpi,
            dpi,
            PixelFormats.Pbgra32);

        // Создаём DrawingVisual для масштабирования
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            var brush = new VisualBrush(canvas)
            {
                Stretch = Stretch.None,
                AlignmentX = AlignmentX.Left,
                AlignmentY = AlignmentY.Top
            };

            context.PushTransform(new ScaleTransform(scale * dpi / 96.0, scale * dpi / 96.0));
            context.DrawRectangle(brush, null, new Rect(0, 0, width, height));
            context.Pop();
        }

        renderBitmap.Render(visual);
        return renderBitmap;
    }

    /// <summary>
    /// Рисует заголовок страницы.
    /// </summary>
    private void DrawPageHeader(XGraphics gfx, PdfExportSettings settings, 
        double x, double y, double width, int pageNum, int totalPages)
    {
        var titleFont = new XFont("Arial", 14, XFontStyle.Bold);
        var subtitleFont = new XFont("Arial", 9, XFontStyle.Regular);

        // Название проекта
        gfx.DrawString(settings.ProjectName, titleFont, XBrushes.Black, 
            new XPoint(x, y + 15));

        // Дата экспорта (справа)
        if (settings.ShowExportDate)
        {
            var dateText = $"Экспорт: {settings.ExportDate:dd.MM.yyyy HH:mm}";
            var dateWidth = gfx.MeasureString(dateText, subtitleFont).Width;
            gfx.DrawString(dateText, subtitleFont, XBrushes.Gray,
                new XPoint(x + width - dateWidth, y + 15));
        }

        // Линия под заголовком
        gfx.DrawLine(new XPen(XColors.LightGray, 0.5), x, y + 25, x + width, y + 25);
    }

    /// <summary>
    /// Рисует подвал страницы с нумерацией.
    /// </summary>
    private void DrawPageFooter(XGraphics gfx, int pageNum, int totalPages, 
        double x, double y, double width)
    {
        var font = new XFont("Arial", 9, XFontStyle.Regular);
        var text = $"Страница {pageNum} из {totalPages}";
        var textWidth = gfx.MeasureString(text, font).Width;

        gfx.DrawString(text, font, XBrushes.Gray,
            new XPoint(x + (width - textWidth) / 2, y));
    }

    /// <summary>
    /// Рисует часть Bitmap в PDF.
    /// </summary>
    private void DrawBitmapSection(XGraphics gfx, BitmapSource source, Int32Rect sourceRect,
        double destX, double destY, double destWidth, double destHeight)
    {
        // Вырезаем нужную часть
        var croppedBitmap = new CroppedBitmap(source, sourceRect);
        
        // Конвертируем в PNG в памяти
        using var stream = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(croppedBitmap));
        encoder.Save(stream);
        stream.Position = 0;

        // Рисуем в PDF
        var xImage = XImage.FromStream(() => new MemoryStream(stream.ToArray()));
        gfx.DrawImage(xImage, destX, destY, destWidth, destHeight);
    }
}
