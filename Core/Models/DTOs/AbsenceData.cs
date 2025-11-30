using System.Text.Json.Serialization;

namespace Core.Models.DTOs;

/// <summary>
/// DTO для сериализации Absence в JSON.
/// </summary>
public class AbsenceData
{
    /// <summary>
    /// Уникальный идентификатор отсутствия.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    /// <summary>
    /// Идентификатор ресурса.
    /// </summary>
    [JsonPropertyName("resourceId")]
    public Guid ResourceId { get; set; }

    /// <summary>
    /// Начало отсутствия в днях (TotalDays).
    /// </summary>
    [JsonPropertyName("startDays")]
    public double StartDays { get; set; }

    /// <summary>
    /// Конец отсутствия в днях (TotalDays).
    /// </summary>
    [JsonPropertyName("endDays")]
    public double EndDays { get; set; }

    /// <summary>
    /// Причина отсутствия (опционально).
    /// </summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    /// <summary>
    /// Дата создания (ISO 8601).
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Конвертирует доменную модель в DTO.
    /// </summary>
    public static AbsenceData FromDomain(Absence absence)
    {
        return new AbsenceData
        {
            Id = absence.Id,
            ResourceId = absence.ResourceId,
            StartDays = absence.Start.TotalDays,
            EndDays = absence.End.TotalDays,
            Reason = absence.Reason,
            CreatedAt = absence.CreatedAt
        };
    }

    /// <summary>
    /// Конвертирует DTO в доменную модель.
    /// </summary>
    public Absence ToDomain()
    {
        return new Absence
        {
            Id = Id,
            ResourceId = ResourceId,
            Start = TimeSpan.FromDays(StartDays),
            End = TimeSpan.FromDays(EndDays),
            Reason = Reason,
            CreatedAt = CreatedAt
        };
    }
}