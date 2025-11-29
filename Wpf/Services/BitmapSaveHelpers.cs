using System.IO;
using System.Windows.Media.Imaging;

namespace Wpf.Services;

public static class BitmapSaveHelpers
{
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

    // Сохраняет как JPEG с возможностью задать качество 1-100
    public static void SaveBitmapToJpeg(BitmapSource bitmap, string filePath, int quality = 90)
    {
        if (bitmap == null) throw new ArgumentNullException(nameof(bitmap));
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var encoder = new JpegBitmapEncoder { QualityLevel = Math.Clamp(quality, 1, 100) };
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            encoder.Save(fs);
        }
    }
}