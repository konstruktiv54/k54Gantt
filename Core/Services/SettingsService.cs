using System.IO;
using Newtonsoft.Json;

namespace Core.Services;

/// <summary>
/// Сервис для управления настройками приложения.
/// Обеспечивает сохранение и загрузку пользовательских настроек, таких как путь к последнему открытому файлу.
/// </summary>
public static class SettingsService
{
    private static string settingsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GanttChart",
        "settings.json"
    );

    /// <summary>
    /// Получает или устанавливает путь к последнему открытому файлу проекта.
    /// При установке значения автоматически сохраняет настройки в файл.
    /// </summary>
    /// <value>Полный путь к файлу или null, если файл не был открыт.</value>
    public static string LastOpenedFile
    {
        get
        {
            try
            {
                if (File.Exists(settingsFile))
                {
                    var json = File.ReadAllText(settingsFile);
                    var settings = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    return settings.ContainsKey("LastOpenedFile") ? settings["LastOpenedFile"] : null;
                }
            }
            catch
            {
                // Игнорируем ошибки чтения настроек
            }
            return null;
        }
        set
        {
            try
            {
                var directory = Path.GetDirectoryName(settingsFile);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var settings = new Dictionary<string, string>
                {
                    ["LastOpenedFile"] = value
                };

                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(settingsFile, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
    }
}