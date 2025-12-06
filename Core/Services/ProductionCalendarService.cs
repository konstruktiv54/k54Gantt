using Core.Models;

namespace Core.Services;

/// <summary>
/// Сервис управления производственным календарём.
/// Хранит глобальные праздничные дни, применяемые ко всем ресурсам проекта.
/// </summary>
public class ProductionCalendarService
{
    private readonly List<Holiday> _holidays = new();

    /// <summary>
    /// Событие, возникающее при изменении списка праздников.
    /// </summary>
    public event EventHandler? HolidaysChanged;

    /// <summary>
    /// Получает список всех праздников (только для чтения).
    /// </summary>
    public IReadOnlyList<Holiday> Holidays => _holidays.AsReadOnly();

    /// <summary>
    /// Получает количество праздников.
    /// </summary>
    public int Count => _holidays.Count;

    /// <summary>
    /// Добавляет праздник в календарь.
    /// </summary>
    /// <param name="holiday">Праздник для добавления.</param>
    public void AddHoliday(Holiday holiday)
    {
        if (holiday == null)
            throw new ArgumentNullException(nameof(holiday));

        // Проверяем на дубликат по дню
        if (_holidays.Any(h => h.Day == holiday.Day))
        {
            System.Diagnostics.Debug.WriteLine(
                $"Holiday already exists for day {holiday.Day.Days}");
            return;
        }

        _holidays.Add(holiday);
        _holidays.Sort((a, b) => a.Day.CompareTo(b.Day));
        
        OnHolidaysChanged();
    }

    /// <summary>
    /// Добавляет праздник по абсолютной дате.
    /// </summary>
    /// <param name="date">Дата праздника.</param>
    /// <param name="projectStart">Дата начала проекта.</param>
    /// <param name="name">Название праздника.</param>
    /// <returns>Созданный праздник.</returns>
    public Holiday AddHoliday(DateTime date, DateTime projectStart, string? name = null)
    {
        var holiday = Holiday.FromDate(date, projectStart, name);
        AddHoliday(holiday);
        return holiday;
    }

    /// <summary>
    /// Удаляет праздник по идентификатору.
    /// </summary>
    /// <param name="holidayId">Идентификатор праздника.</param>
    /// <returns>True, если праздник был удалён.</returns>
    public bool RemoveHoliday(Guid holidayId)
    {
        var holiday = _holidays.FirstOrDefault(h => h.Id == holidayId);
        if (holiday == null)
            return false;

        _holidays.Remove(holiday);
        OnHolidaysChanged();
        return true;
    }

    /// <summary>
    /// Удаляет праздник по дню.
    /// </summary>
    /// <param name="day">День праздника относительно начала проекта.</param>
    /// <returns>True, если праздник был удалён.</returns>
    public bool RemoveHolidayByDay(TimeSpan day)
    {
        var holiday = _holidays.FirstOrDefault(h => h.Day == day);
        if (holiday == null)
            return false;

        _holidays.Remove(holiday);
        OnHolidaysChanged();
        return true;
    }

    /// <summary>
    /// Проверяет, является ли указанный день праздником.
    /// </summary>
    /// <param name="day">День относительно начала проекта.</param>
    /// <returns>True, если день является праздником.</returns>
    public bool IsHoliday(TimeSpan day)
    {
        // Округляем до целых дней для корректного сравнения
        var dayNumber = (int)Math.Round(day.TotalDays);
        return _holidays.Any(h => (int)Math.Round(h.Day.TotalDays) == dayNumber);
    }

    /// <summary>
    /// Проверяет, является ли указанная дата праздником.
    /// </summary>
    /// <param name="date">Абсолютная дата.</param>
    /// <param name="projectStart">Дата начала проекта.</param>
    /// <returns>True, если дата является праздником.</returns>
    public bool IsHoliday(DateTime date, DateTime projectStart)
    {
        var dayOffset = (date.Date - projectStart.Date).Days;
        return IsHoliday(TimeSpan.FromDays(dayOffset));
    }

    /// <summary>
    /// Получает праздник по указанному дню.
    /// </summary>
    /// <param name="day">День относительно начала проекта.</param>
    /// <returns>Праздник или null, если не найден.</returns>
    public Holiday? GetHoliday(TimeSpan day)
    {
        var dayNumber = (int)Math.Round(day.TotalDays);
        return _holidays.FirstOrDefault(h => (int)Math.Round(h.Day.TotalDays) == dayNumber);
    }

    /// <summary>
    /// Получает все праздники в указанном диапазоне дней.
    /// </summary>
    /// <param name="start">Начало диапазона (включительно).</param>
    /// <param name="end">Конец диапазона (включительно).</param>
    /// <returns>Список праздников в диапазоне.</returns>
    public List<Holiday> GetHolidaysInRange(TimeSpan start, TimeSpan end)
    {
        var startDays = (int)Math.Round(start.TotalDays);
        var endDays = (int)Math.Round(end.TotalDays);

        return _holidays
            .Where(h =>
            {
                var holidayDays = (int)Math.Round(h.Day.TotalDays);
                return holidayDays >= startDays && holidayDays <= endDays;
            })
            .OrderBy(h => h.Day)
            .ToList();
    }

    /// <summary>
    /// Загружает праздники из коллекции (используется при десериализации).
    /// </summary>
    /// <param name="holidays">Коллекция праздников для загрузки.</param>
    public void LoadHolidays(IEnumerable<Holiday> holidays)
    {
        _holidays.Clear();
        
        if (holidays != null)
        {
            _holidays.AddRange(holidays);
            _holidays.Sort((a, b) => a.Day.CompareTo(b.Day));
        }

        OnHolidaysChanged();
    }

    /// <summary>
    /// Очищает все праздники.
    /// </summary>
    public void Clear()
    {
        _holidays.Clear();
        OnHolidaysChanged();
    }

    /// <summary>
    /// Получает все праздники для сериализации.
    /// </summary>
    /// <returns>Копия списка праздников.</returns>
    public List<Holiday> GetAllHolidays()
    {
        return new List<Holiday>(_holidays);
    }

    /// <summary>
    /// Вызывает событие HolidaysChanged.
    /// </summary>
    protected virtual void OnHolidaysChanged()
    {
        HolidaysChanged?.Invoke(this, EventArgs.Empty);
    }
}