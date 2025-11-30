namespace Core.Models.DTOs;

/// <summary>
/// DTO для сериализации Resource в JSON.
/// </summary>
[Serializable]
public class ResourceData
{
    /// <summary>
    /// Уникальный идентификатор ресурса.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Имя ресурса.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Инициалы ресурса.
    /// </summary>
    public string Initials { get; set; }

    /// <summary>
    /// Цвет в формате HEX.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Роль ресурса (int представление ResourceRole enum).
    /// 0 = Constructor, 1 = LeadSpecialist, 2 = ChiefConstructor.
    /// </summary>
    public int Role { get; set; }

    /// <summary>
    /// [LEGACY] Строковое представление роли для обратной совместимости.
    /// Используется только при чтении старых файлов.
    /// При записи не используется.
    /// </summary>
    [Obsolete("Use Role (int) instead. This field is for legacy file migration only.")]
    public string? RoleLegacy { get; set; }

    /// <summary>
    /// [LEGACY] Максимальная загрузка - перенесена в ParticipationInterval.
    /// Это поле сохранено только для миграции старых файлов.
    /// </summary>
    [Obsolete("MaxWorkload moved to ParticipationInterval. This field is for legacy file migration only.")]
    public int MaxWorkload { get; set; } = 100;

    public ResourceData()
    {
        Color = "#4682B4";
        Role = 0; // Constructor by default
    }
}