namespace Core.Models;

/// <summary>
/// Интервал участия ресурса в проекте.
/// Определяет период, когда ресурс доступен, и его максимальную нагрузку.
/// Использует half-open интервал [Start, End).
/// </summary>
public class ParticipationInterval
{
    /// <summary>
    /// Уникальный идентификатор интервала.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Идентификатор ресурса, которому принадлежит интервал.
    /// </summary>
    public Guid ResourceId { get; set; }

    /// <summary>
    /// Начало интервала (включительно).
    /// Измеряется в днях от начала проекта.
    /// </summary>
    public TimeSpan Start { get; set; }

    /// <summary>
    /// Конец интервала (не включительно).
    /// Null означает бесконечный интервал (до конца проекта).
    /// Измеряется в днях от начала проекта.
    /// </summary>
    public TimeSpan? End { get; set; }

    /// <summary>
    /// Максимальная нагрузка ресурса в этот период (0-100%).
    /// Для роли Constructor всегда должно быть 100.
    /// </summary>
    public int MaxWorkload { get; set; } = 100;

    /// <summary>
    /// Дата создания записи (для сортировки и аудита).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Создаёт новый интервал участия.
    /// </summary>
    public ParticipationInterval()
    {
        Id = Guid.NewGuid();
    }

    /// <summary>
    /// Создаёт новый интервал участия с указанными параметрами.
    /// </summary>
    /// <param name="resourceId">ID ресурса.</param>
    /// <param name="start">Начало интервала.</param>
    /// <param name="end">Конец интервала (null = бесконечный).</param>
    /// <param name="maxWorkload">Максимальная нагрузка (0-100).</param>
    public ParticipationInterval(Guid resourceId, TimeSpan start, TimeSpan? end, int maxWorkload = 100)
        : this()
    {
        ResourceId = resourceId;
        Start = start;
        End = end;
        MaxWorkload = Math.Clamp(maxWorkload, 0, 100);
    }

    /// <summary>
    /// Создаёт дефолтный интервал для нового ресурса.
    /// Start = 0, End = null (бесконечный), MaxWorkload = 100.
    /// </summary>
    /// <param name="resourceId">ID ресурса.</param>
    /// <returns>Дефолтный интервал участия.</returns>
    public static ParticipationInterval CreateDefault(Guid resourceId)
    {
        return new ParticipationInterval(resourceId, TimeSpan.Zero, null, 100);
    }

    /// <summary>
    /// Проверяет, содержит ли интервал указанный день.
    /// Использует half-open семантику: [Start, End).
    /// </summary>
    /// <param name="day">День для проверки.</param>
    /// <returns>True, если день входит в интервал.</returns>
    public bool ContainsDay(TimeSpan day)
    {
        if (day < Start)
            return false;

        if (End.HasValue && day >= End.Value)
            return false;

        return true;
    }

    /// <summary>
    /// Проверяет, пересекается ли интервал с другим интервалом.
    /// </summary>
    /// <param name="other">Другой интервал.</param>
    /// <returns>True, если интервалы пересекаются.</returns>
    public bool OverlapsWith(ParticipationInterval other)
    {
        if (other == null)
            return false;

        // Если это тот же интервал, не считаем пересечением
        if (Id == other.Id)
            return false;

        // Интервалы разных ресурсов не проверяем
        if (ResourceId != other.ResourceId)
            return false;

        // [A.Start, A.End) пересекается с [B.Start, B.End)?
        // Пересечение: A.Start < B.End && B.Start < A.End

        var thisEnd = End ?? TimeSpan.MaxValue;
        var otherEnd = other.End ?? TimeSpan.MaxValue;

        return Start < otherEnd && other.Start < thisEnd;
    }

    /// <summary>
    /// Проверяет, пересекается ли интервал с диапазоном дней.
    /// </summary>
    /// <param name="rangeStart">Начало диапазона.</param>
    /// <param name="rangeEnd">Конец диапазона.</param>
    /// <returns>True, если есть пересечение.</returns>
    public bool OverlapsWithRange(TimeSpan rangeStart, TimeSpan rangeEnd)
    {
        var thisEnd = End ?? TimeSpan.MaxValue;
        return Start < rangeEnd && rangeStart < thisEnd;
    }

    /// <summary>
    /// Возвращает строковое представление интервала.
    /// </summary>
    public override string ToString()
    {
        var endStr = End.HasValue ? $"День {End.Value.Days}" : "∞";
        return $"[День {Start.Days} — {endStr}], MaxWorkload: {MaxWorkload}%";
    }

    /// <summary>
    /// Валидирует интервал.
    /// </summary>
    /// <returns>Список ошибок валидации (пустой, если валидно).</returns>
    public List<string> Validate()
    {
        var errors = new List<string>();

        if (ResourceId == Guid.Empty)
            errors.Add("ResourceId не может быть пустым.");

        if (Start < TimeSpan.Zero)
            errors.Add("Start не может быть отрицательным.");

        if (End.HasValue && End.Value <= Start)
            errors.Add("End должен быть больше Start.");

        if (MaxWorkload < 0 || MaxWorkload > 100)
            errors.Add("MaxWorkload должен быть в диапазоне 0-100.");

        return errors;
    }
}
 