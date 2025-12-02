namespace Core.Models;

/// <summary>
/// Вычисляемый статус дня для ресурса.
/// Агрегирует информацию о нагрузке, отсутствии и участии.
/// Не сохраняется в файл — генерируется по запросу.
/// </summary>
public class DayStatus
{
    /// <summary>
    /// День (смещение от начала проекта).
    /// </summary>
    public TimeSpan Day { get; }

    /// <summary>
    /// Идентификатор ресурса.
    /// </summary>
    public Guid ResourceId { get; }

    /// <summary>
    /// Участвует ли ресурс в проекте в этот день.
    /// True, если день попадает в ParticipationInterval.
    /// </summary>
    public bool InParticipation { get; }

    /// <summary>
    /// Максимальная нагрузка из ParticipationInterval.
    /// 0, если ресурс не участвует.
    /// </summary>
    public int MaxWorkload { get; }

    /// <summary>
    /// Отсутствует ли ресурс в этот день.
    /// True, если день попадает в Absence.
    /// </summary>
    public bool InAbsence { get; }

    /// <summary>
    /// Причина отсутствия (если InAbsence = true).
    /// </summary>
    public string? AbsenceReason { get; }

    /// <summary>
    /// Назначения ресурса на этот день.
    /// </summary>
    public IReadOnlyList<ResourceAssignment> Assignments { get; }

    /// <summary>
    /// Расчётный процент загрузки.
    /// Сумма: Assignment.Workload × RoleCoefficient для всех назначений.
    /// </summary>
    public int AllocationPercent { get; }

    /// <summary>
    /// Итоговое состояние дня.
    /// </summary>
    public DayState State { get; }

    /// <summary>
    /// Создаёт статус дня.
    /// </summary>
    /// <param name="day">День.</param>
    /// <param name="resourceId">ID ресурса.</param>
    /// <param name="inParticipation">Участвует в проекте.</param>
    /// <param name="maxWorkload">Максимальная нагрузка.</param>
    /// <param name="inAbsence">Отсутствует.</param>
    /// <param name="absenceReason">Причина отсутствия.</param>
    /// <param name="assignments">Назначения на день.</param>
    /// <param name="allocationPercent">Процент загрузки.</param>
    /// <param name="state">Состояние дня.</param>
    public DayStatus(
        TimeSpan day,
        Guid resourceId,
        bool inParticipation,
        int maxWorkload,
        bool inAbsence,
        string? absenceReason,
        IReadOnlyList<ResourceAssignment> assignments,
        int allocationPercent,
        DayState state)
    {
        Day = day;
        ResourceId = resourceId;
        InParticipation = inParticipation;
        MaxWorkload = maxWorkload;
        InAbsence = inAbsence;
        AbsenceReason = absenceReason;
        Assignments = assignments ?? Array.Empty<ResourceAssignment>();
        AllocationPercent = allocationPercent;
        State = state;
    }

    /// <summary>
    /// Возвращает процент заполнения для визуализации (0.0 - 1.0).
    /// Используется для прозрачности цвета.
    /// </summary>
    public double FillRatio
    {
        get
        {
            if (MaxWorkload <= 0)
                return 0;

            return Math.Min(1.0, (double)AllocationPercent / MaxWorkload);
        }
    }

    /// <summary>
    /// Проверяет, есть ли у ресурса резерв для назначения.
    /// </summary>
    public int AvailableCapacity => Math.Max(0, MaxWorkload - AllocationPercent);

    /// <summary>
    /// Возвращает текст для tooltip.
    /// </summary>
    public string TooltipText
    {
        get
        {
            var lines = new List<string>
            {
                $"День {Day.Days}",
                $"Статус: {State.GetDisplayName()}"
            };

            if (InAbsence && !string.IsNullOrWhiteSpace(AbsenceReason))
            {
                lines.Add($"Отсутствие: {AbsenceReason}");
            }

            if (InParticipation)
            {
                lines.Add($"Загрузка: {AllocationPercent}% / {MaxWorkload}%");
            }

            if (Assignments.Count > 0)
            {
                lines.Add($"Назначений: {Assignments.Count}");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    /// <summary>
    /// Возвращает строковое представление.
    /// </summary>
    public override string ToString()
    {
        return $"День {Day.Days}: {State.GetDisplayName()}, {AllocationPercent}%/{MaxWorkload}%";
    }

    /// <summary>
    /// Создаёт статус для дня, когда ресурс не участвует в проекте.
    /// </summary>
    public static DayStatus NotParticipating(TimeSpan day, Guid resourceId)
    {
        return new DayStatus(
            day: day,
            resourceId: resourceId,
            inParticipation: false,
            maxWorkload: 0,
            inAbsence: false,
            absenceReason: null,
            assignments: Array.Empty<ResourceAssignment>(),
            allocationPercent: 0,
            state: DayState.NotParticipating);
    }
    
    /// <summary>
    /// Создаёт статус для выходного дня.
    /// Назначения игнорируются, загрузка = 0.
    /// </summary>
    public static DayStatus Weekend(TimeSpan day, Guid resourceId)
    {
        return new DayStatus(
            day: day,
            resourceId: resourceId,
            inParticipation: false,  // В выходные не работаем
            maxWorkload: 0,
            inAbsence: false,
            absenceReason: null,
            assignments: Array.Empty<ResourceAssignment>(),
            allocationPercent: 0,
            state: DayState.Weekend);
    }

    /// <summary>
    /// Создаёт статус для свободного дня.
    /// </summary>
    public static DayStatus Free(TimeSpan day, Guid resourceId, int maxWorkload)
    {
        return new DayStatus(
            day: day,
            resourceId: resourceId,
            inParticipation: true,
            maxWorkload: maxWorkload,
            inAbsence: false,
            absenceReason: null,
            assignments: Array.Empty<ResourceAssignment>(),
            allocationPercent: 0,
            state: DayState.Free);
    }
}
