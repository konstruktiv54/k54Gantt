namespace Core.Models.DTOs;

/// <summary>
/// DTO для сериализации ParticipationInterval в JSON.
/// </summary>
[Serializable]
public class ParticipationIntervalData
{
    /// <summary>
    /// Уникальный идентификатор интервала.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Идентификатор ресурса.
    /// </summary>
    public Guid ResourceId { get; set; }

    /// <summary>
    /// Начало интервала в днях (TotalDays).
    /// </summary>
    public double StartDays { get; set; }

    /// <summary>
    /// Конец интервала в днях (TotalDays).
    /// Null означает бесконечный интервал.
    /// </summary>
    public double? EndDays { get; set; }

    /// <summary>
    /// Максимальная нагрузка (0-100).
    /// </summary>
    public int MaxWorkload { get; set; } = 100;

    /// <summary>
    /// Дата создания.
    /// </summary>
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