using System.Text.Json.Serialization;

namespace Core.Models.DTOs;

/// <summary>
/// DTO для сериализации ParticipationInterval в JSON.
/// </summary>
public class ParticipationIntervalData
{
    /// <summary>
    /// Уникальный идентификатор интервала.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    /// <summary>
    /// Идентификатор ресурса.
    /// </summary>
    [JsonPropertyName("resourceId")]
    public Guid ResourceId { get; set; }

    /// <summary>
    /// Начало интервала в днях (TotalDays).
    /// </summary>
    [JsonPropertyName("startDays")]
    public double StartDays { get; set; }

    /// <summary>
    /// Конец интервала в днях (TotalDays).
    /// Null означает бесконечный интервал.
    /// </summary>
    [JsonPropertyName("endDays")]
    public double? EndDays { get; set; }

    /// <summary>
    /// Максимальная нагрузка (0-100).
    /// </summary>
    [JsonPropertyName("maxWorkload")]
    public int MaxWorkload { get; set; } = 100;

    /// <summary>
    /// Дата создания (ISO 8601).
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Конвертирует доменную модель в DTO.
    /// </summary>
    public static ParticipationIntervalData FromDomain(ParticipationInterval interval)
    {
        return new ParticipationIntervalData
        {
            Id = interval.Id,
            ResourceId = interval.ResourceId,
            StartDays = interval.Start.TotalDays,
            EndDays = interval.End?.TotalDays,
            MaxWorkload = interval.MaxWorkload,
            CreatedAt = interval.CreatedAt
        };
    }

    /// <summary>
    /// Конвертирует DTO в доменную модель.
    /// </summary>
    public ParticipationInterval ToDomain()
    {
        return new ParticipationInterval
        {
            Id = Id,
            ResourceId = ResourceId,
            Start = TimeSpan.FromDays(StartDays),
            End = EndDays.HasValue ? TimeSpan.FromDays(EndDays.Value) : null,
            MaxWorkload = MaxWorkload,
            CreatedAt = CreatedAt
        };
    }
}
