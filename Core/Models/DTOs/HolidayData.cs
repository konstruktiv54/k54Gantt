namespace Core.Models.DTOs;

/// <summary>
/// DTO для сериализации праздничного дня.
/// </summary>
[Serializable]
public class HolidayData
{
    /// <summary>
    /// Уникальный идентификатор праздника.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// День праздника как смещение в днях от начала проекта.
    /// </summary>
    public int DayOffset { get; set; }

    /// <summary>
    /// Название праздника (опционально).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Дата создания записи.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Создаёт DTO из доменной модели.
    /// </summary>
    public static HolidayData FromDomain(Holiday holiday)
    {
        System.Diagnostics.Debug.WriteLine($"FromDomain: Day.TotalDays = {holiday.Day.TotalDays}, Day.Days = {holiday.Day.Days}");
        return new HolidayData
        {
            Id = holiday.Id,
            DayOffset = holiday.Day.Days,
            Name = holiday.Name,
            CreatedAt = holiday.CreatedAt
        };
    }

    /// <summary>
    /// Преобразует DTO в доменную модель.
    /// </summary>
    public Holiday ToDomain()
    {
        return new Holiday
        {
            Id = Id,
            Day = TimeSpan.FromDays(DayOffset),
            Name = Name,
            CreatedAt = CreatedAt
        };
    }
}