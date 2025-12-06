namespace Core.Models;

/// <summary>
/// Статус дня для ресурса.
/// Определяет визуальное отображение в Resource Engagement Strip.
/// </summary>
public enum DayState
{
    /// <summary>
    /// Ресурс свободен (AllocationPercent == 0, не в отсутствии, участвует в проекте).
    /// Визуализация: нейтральный фон.
    /// </summary>
    Free = 0,

    /// <summary>
    /// Ресурс отсутствует (отпуск, болезнь, командировка) и нет назначений.
    /// Визуализация: серый с диагональной штриховкой.
    /// </summary>
    Absence = 1,

    /// <summary>
    /// Ресурс не участвует в проекте в этот период (вне ParticipationInterval).
    /// Визуализация: светло-серый фон.
    /// </summary>
    NotParticipating = 2,

    /// <summary>
    /// Частично занят (0 &lt; AllocationPercent &lt; MaxWorkload).
    /// Визуализация: цвет ресурса с прозрачностью (пропорционально загрузке).
    /// </summary>
    PartialAssigned = 3,

    /// <summary>
    /// Полностью занят (AllocationPercent == MaxWorkload).
    /// Визуализация: цвет ресурса без прозрачности.
    /// </summary>
    Assigned = 4,

    /// <summary>
    /// Перегружен: AllocationPercent > MaxWorkload, 
    /// или есть назначения в период отсутствия/неучастия.
    /// Визуализация: цвет ресурса + красный контур.
    /// </summary>
    Overbooked = 5,

    /// <summary>
    /// Выходной день (суббота или воскресенье).
    /// Назначения игнорируются, загрузка = 0.
    /// Визуализация: светло-коричневая диагональная штриховка.
    /// </summary>
    Weekend = 6
}

/// <summary>
/// Расширения для DayState.
/// </summary>
public static class DayStateExtensions
{
    /// <summary>
    /// Возвращает локализованное название статуса.
    /// </summary>
    /// <param name="state">Статус дня.</param>
    /// <returns>Название на русском языке.</returns>
    public static string GetDisplayName(this DayState state)
    {
        return state switch
        {
            DayState.Free => "Свободен",
            DayState.Absence => "Отсутствует",
            DayState.NotParticipating => "Не участвует",
            DayState.PartialAssigned => "Частично занят",
            DayState.Assigned => "Занят",
            DayState.Overbooked => "Перегружен",
            DayState.Weekend => "Выходной день",
            _ => "Неизвестно"
        };
    }

    /// <summary>
    /// Возвращает приоритет статуса (для разрешения конфликтов).
    /// Меньшее значение = выше приоритет.
    /// </summary>
    /// <param name="state">Статус дня.</param>
    /// <returns>Приоритет (1-7).</returns>
    public static int GetPriority(this DayState state)
    {
        return state switch
        {
            DayState.Weekend => 1,          // Наивысший — выходные
            DayState.Absence => 2,          // Второй — отсутствие (отпуск, больничный)
            DayState.Overbooked => 3,
            DayState.Assigned => 4,
            DayState.PartialAssigned => 5,
            DayState.NotParticipating => 6,
            DayState.Free => 7,             // Низший приоритет
            _ => 7
        };
    }

    /// <summary>
    /// Проверяет, является ли статус проблемным (требует внимания).
    /// </summary>
    /// <param name="state">Статус дня.</param>
    /// <returns>True для Overbooked.</returns>
    public static bool IsProblematic(this DayState state)
    {
        return state == DayState.Overbooked;
    }

    /// <summary>
    /// Проверяет, доступен ли ресурс для назначения.
    /// </summary>
    /// <param name="state">Статус дня.</param>
    /// <returns>True, если ресурс может принять назначение без конфликта.</returns>
    public static bool IsAvailable(this DayState state)
    {
        return state == DayState.Free || state == DayState.PartialAssigned;
    }

    /// <summary>
    /// Проверяет, является ли день нерабочим (выходной или отсутствие).
    /// </summary>
    /// <param name="state">Статус дня.</param>
    /// <returns>True для Weekend, Absence, NotParticipating.</returns>
    public static bool IsNonWorking(this DayState state)
    {
        return state == DayState.Weekend 
            || state == DayState.Absence 
            || state == DayState.NotParticipating;
    }
}