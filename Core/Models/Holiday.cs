namespace Core.Models;

/// <summary>
/// Представляет праздничный день в производственном календаре.
/// Праздники применяются глобально ко всем ресурсам проекта.
/// </summary>
public class Holiday
{
    /// <summary>
    /// Уникальный идентификатор праздника.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// День праздника относительно начала проекта (как TimeSpan в днях).
    /// Аналогично Absence.Start для единообразия.
    /// </summary>
    public TimeSpan Day { get; set; }

    /// <summary>
    /// Название праздника (опционально).
    /// Например: "Новый год", "8 марта", "День Победы".
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Дата создания записи.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Создаёт пустой праздник.
    /// </summary>
    public Holiday()
    {
    }

    /// <summary>
    /// Создаёт праздник с указанным днём и названием.
    /// </summary>
    /// <param name="day">День относительно начала проекта.</param>
    /// <param name="name">Название праздника.</param>
    public Holiday(TimeSpan day, string? name = null)
    {
        Day = day;
        Name = name;
    }

    /// <summary>
    /// Создаёт праздник из абсолютной даты.
    /// </summary>
    /// <param name="date">Абсолютная дата праздника.</param>
    /// <param name="projectStart">Дата начала проекта.</param>
    /// <param name="name">Название праздника.</param>
    public static Holiday FromDate(DateTime date, DateTime projectStart, string? name = null)
    {
        var dayOffset = (date.Date - projectStart.Date).Days;
        System.Diagnostics.Debug.WriteLine($"FromDate: {date:yyyy-MM-dd} - {projectStart:yyyy-MM-dd} = {dayOffset} дней");
        return new Holiday(TimeSpan.FromDays(dayOffset), name);
    }

    /// <summary>
    /// Получает абсолютную дату праздника.
    /// </summary>
    /// <param name="projectStart">Дата начала проекта.</param>
    /// <returns>Абсолютная дата праздника.</returns>
    public DateTime GetDate(DateTime projectStart)
    {
        return projectStart.Date.AddDays(Day.Days);
    }

    public override string ToString()
    {
        return string.IsNullOrEmpty(Name) 
            ? $"Holiday at day {Day.Days}" 
            : $"{Name} (day {Day.Days})";
    }
}