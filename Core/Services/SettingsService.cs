using System.IO;
using Newtonsoft.Json;

namespace Core.Services;

/// <summary>
/// Сервис для управления настройками приложения.
/// Обеспечивает сохранение и загрузку пользовательских настроек, таких как путь к последнему открытому файлу.
/// </summary>
public static class SettingsService
{
    
    private static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GanttChart");
    
    private static string SettingsFile = Path.Combine(
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
                if (File.Exists(SettingsFile))
                {
                    var json = File.ReadAllText(SettingsFile);
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
                var directory = Path.GetDirectoryName(SettingsFile);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var settings = new Dictionary<string, string>
                {
                    ["LastOpenedFile"] = value
                };

                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(SettingsFile, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
    }
    
        /// <summary>
    /// Масштаб по умолчанию.
    /// </summary>
    public static int DefaultZoomLevel
    {
        get
        {
            var value = GetSetting("DefaultZoomLevel");
            return int.TryParse(value, out var zoom) ? zoom : 100;
        }
        set => SetSetting("DefaultZoomLevel", value.ToString());
    }

    /// <summary>
    /// Показывать линию "Сегодня".
    /// </summary>
    public static bool ShowTodayLine
    {
        get
        {
            var value = GetSetting("ShowTodayLine");
            return string.IsNullOrEmpty(value) || bool.TryParse(value, out var show) && show;
        }
        set => SetSetting("ShowTodayLine", value.ToString());
    }

    /// <summary>
    /// Показывать выходные дни.
    /// </summary>
    public static bool HighlightWeekends
    {
        get
        {
            var value = GetSetting("HighlightWeekends");
            return string.IsNullOrEmpty(value) || bool.TryParse(value, out var show) && show;
        }
        set => SetSetting("HighlightWeekends", value.ToString());
    }

    private static string GetSetting(string key)
    {
        try
        {
            if (!File.Exists(SettingsFile))
                return string.Empty;

            var lines = File.ReadAllLines(SettingsFile);
            foreach (var line in lines)
            {
                var parts = line.Split('=');
                if (parts.Length == 2 && parts[0].Trim() == key)
                    return parts[1].Trim();
            }
        }
        catch
        {
            // Ignore errors
        }

        return string.Empty;
    }

    private static void SetSetting(string key, string value)
    {
        try
        {
            // Ensure directory exists
            if (!Directory.Exists(SettingsFolder))
                Directory.CreateDirectory(SettingsFolder);

            var settings = new Dictionary<string, string>();

            // Load existing settings
            if (File.Exists(SettingsFile))
            {
                var lines = File.ReadAllLines(SettingsFile);
                foreach (var line in lines)
                {
                    var parts = line.Split('=');
                    if (parts.Length == 2)
                        settings[parts[0].Trim()] = parts[1].Trim();
                }
            }

            // Update setting
            settings[key] = value;

            // Save settings
            var output = settings.Select(kvp => $"{kvp.Key}={kvp.Value}");
            File.WriteAllLines(SettingsFile, output);
        }
        catch
        {
            // Ignore errors
        }
    }
}