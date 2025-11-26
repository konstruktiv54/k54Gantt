using Newtonsoft.Json;

namespace Core.Converters;

/// <summary>
/// Пользовательский JSON конвертер для сериализации и десериализации TimeSpan.
/// Преобразует TimeSpan в строковое представление и обратно.
/// </summary>
/// <remarks>
/// Используется для корректного сохранения и загрузки временных интервалов в JSON файлах.
/// При ошибке парсинга возвращает TimeSpan.Zero вместо генерации исключения.
/// </remarks>
public class TimeSpanConverter : JsonConverter<TimeSpan>
{
    /// <summary>
    /// Читает TimeSpan из JSON.
    /// </summary>
    /// <param name="reader">JSON reader для чтения данных.</param>
    /// <param name="objectType">Тип объекта (TimeSpan).</param>
    /// <param name="existingValue">Существующее значение TimeSpan.</param>
    /// <param name="hasExistingValue">Указывает, есть ли существующее значение.</param>
    /// <param name="serializer">JSON serializer.</param>
    /// <returns>
    /// Распарсенный TimeSpan или TimeSpan.Zero, если парсинг не удался.
    /// </returns>
    public override TimeSpan ReadJson(JsonReader reader, Type objectType, TimeSpan existingValue,
        bool hasExistingValue, JsonSerializer serializer)
    {
        if (TimeSpan.TryParse(reader.Value?.ToString(), out var result))
            return result;
        return TimeSpan.Zero;
    }

    /// <summary>
    /// Записывает TimeSpan в JSON как строку.
    /// </summary>
    /// <param name="writer">JSON writer для записи данных.</param>
    /// <param name="value">Значение TimeSpan для записи.</param>
    /// <param name="serializer">JSON serializer.</param>
    public override void WriteJson(JsonWriter writer, TimeSpan value, JsonSerializer serializer)
    {
        writer.WriteValue(value.ToString());
    }
}