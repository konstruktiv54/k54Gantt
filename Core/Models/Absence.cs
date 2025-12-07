// Core/Models/Absence.cs
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Core.Models;

/// <summary>
/// Отсутствие ресурса (отпуск, болезнь, командировка и т.д.).
/// Не блокирует назначения, но создаёт статус Overbooked при наличии назначений.
/// Использует half-open интервал [Start, End).
/// </summary>
public class Absence : INotifyPropertyChanged
{
    private TimeSpan _start;
    private TimeSpan _end;
    private string? _reason;

    /// <summary>
    /// Уникальный идентификатор отсутствия.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Идентификатор ресурса, которому принадлежит отсутствие.
    /// </summary>
    public Guid ResourceId { get; set; }

    /// <summary>
    /// Начало отсутствия (включительно).
    /// Измеряется в днях от начала проекта.
    /// При изменении автоматически корректирует End, если End &lt; Start.
    /// </summary>
    public TimeSpan Start
    {
        get => _start;
        set
        {
            if (_start == value) return;
            
            _start = value;
            OnPropertyChanged();
            
            // Валидация: End должен быть >= Start + 1 день
            if (_end <= _start)
            {
                End = _start + TimeSpan.FromDays(1);
            }
        }
    }

    /// <summary>
    /// Конец отсутствия (не включительно).
    /// Измеряется в днях от начала проекта.
    /// Автоматически корректируется, если меньше Start.
    /// </summary>
    public TimeSpan End
    {
        get => _end;
        set
        {
            // Валидация: End не может быть <= Start
            var correctedValue = value <= _start 
                ? _start + TimeSpan.FromDays(1) 
                : value;
            
            if (_end == correctedValue) return;
            
            _end = correctedValue;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Причина отсутствия (опционально).
    /// Например: "Отпуск", "Болезнь", "Командировка".
    /// </summary>
    public string? Reason
    {
        get => _reason;
        set
        {
            if (_reason == value) return;
            _reason = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Дата создания записи (для сортировки и аудита).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Создаёт новое отсутствие.
    /// </summary>
    public Absence()
    {
        Id = Guid.NewGuid();
        _end = TimeSpan.FromDays(1); // Минимальная длительность 1 день
    }

    /// <summary>
    /// Создаёт новое отсутствие с указанными параметрами.
    /// </summary>
    /// <param name="resourceId">ID ресурса.</param>
    /// <param name="start">Начало отсутствия.</param>
    /// <param name="end">Конец отсутствия.</param>
    /// <param name="reason">Причина (опционально).</param>
    public Absence(Guid resourceId, TimeSpan start, TimeSpan end, string? reason = null)
        : this()
    {
        ResourceId = resourceId;
        _start = start;
        // Применяем валидацию при создании
        _end = end <= start ? start + TimeSpan.FromDays(1) : end;
        _reason = reason;
    }

    /// <summary>
    /// Проверяет, содержит ли отсутствие указанный день.
    /// Использует half-open семантику: [Start, End).
    /// </summary>
    /// <param name="day">День для проверки.</param>
    /// <returns>True, если день попадает в период отсутствия.</returns>
    public bool ContainsDay(TimeSpan day)
    {
        return day >= Start && day < End;
    }

    /// <summary>
    /// Проверяет, пересекается ли отсутствие с другим отсутствием.
    /// </summary>
    /// <param name="other">Другое отсутствие.</param>
    /// <returns>True, если периоды пересекаются.</returns>
    public bool OverlapsWith(Absence other)
    {
        if (other == null)
            return false;

        // Если это то же отсутствие, не считаем пересечением
        if (Id == other.Id)
            return false;

        // Отсутствия разных ресурсов не проверяем
        if (ResourceId != other.ResourceId)
            return false;

        // [A.Start, A.End) пересекается с [B.Start, B.End)?
        return Start < other.End && other.Start < End;
    }

    /// <summary>
    /// Проверяет, пересекается ли отсутствие с диапазоном дней.
    /// </summary>
    /// <param name="rangeStart">Начало диапазона.</param>
    /// <param name="rangeEnd">Конец диапазона.</param>
    /// <returns>True, если есть пересечение.</returns>
    public bool OverlapsWithRange(TimeSpan rangeStart, TimeSpan rangeEnd)
    {
        return Start < rangeEnd && rangeStart < End;
    }

    /// <summary>
    /// Возвращает длительность отсутствия в днях.
    /// </summary>
    public int DurationDays => (End - Start).Days;

    /// <summary>
    /// Возвращает строковое представление отсутствия.
    /// </summary>
    public override string ToString()
    {
        var reasonStr = string.IsNullOrWhiteSpace(Reason) ? "" : $" ({Reason})";
        return $"[День {Start.Days} — День {End.Days}]{reasonStr}";
    }

    /// <summary>
    /// Валидирует отсутствие.
    /// </summary>
    /// <returns>Список ошибок валидации (пустой, если валидно).</returns>
    public List<string> Validate()
    {
        var errors = new List<string>();

        if (ResourceId == Guid.Empty)
            errors.Add("ResourceId не может быть пустым.");

        if (Start < TimeSpan.Zero)
            errors.Add("Start не может быть отрицательным.");

        if (End <= Start)
            errors.Add("End должен быть больше Start.");

        return errors;
    }

    /// <summary>
    /// Стандартные причины отсутствия для UI.
    /// </summary>
    public static class CommonReasons
    {
        public const string Vacation = "Отпуск";
        public const string Sick = "Болезнь";
        public const string BusinessTrip = "Командировка";
        public const string DayOff = "Отгул";
        public const string Other = "Другое";

        /// <summary>
        /// Возвращает список стандартных причин.
        /// </summary>
        public static IReadOnlyList<string> All => new[]
        {
            Vacation,
            Sick,
            BusinessTrip,
            DayOff,
            Other
        };
    }

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}