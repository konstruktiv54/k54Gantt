namespace Core.Models;

/// <summary>
/// Роль ресурса в проекте.
/// Определяет коэффициент нагрузки при назначении на задачи.
/// </summary>
public enum ResourceRole
{
    /// <summary>
    /// Конструктор. Коэффициент нагрузки: 1.0 (100%).
    /// Полная занятость на задаче.
    /// </summary>
    Constructor = 0,

    /// <summary>
    /// Главный специалист. Коэффициент нагрузки: 0.25 (25%).
    /// Частичный контроль и консультации.
    /// </summary>
    LeadSpecialist = 1,

    /// <summary>
    /// Главный конструктор. Коэффициент нагрузки: 0.10 (10%).
    /// Общий надзор и согласование.
    /// </summary>
    ChiefConstructor = 2
}

/// <summary>
/// Расширения для ResourceRole.
/// </summary>
public static class ResourceRoleExtensions
{
    /// <summary>
    /// Возвращает коэффициент нагрузки для роли.
    /// </summary>
    /// <param name="role">Роль ресурса.</param>
    /// <returns>Коэффициент от 0.0 до 1.0.</returns>
    public static double GetCoefficient(this ResourceRole role)
    {
        return role switch
        {
            ResourceRole.Constructor => 1.0,
            ResourceRole.LeadSpecialist => 0.25,
            ResourceRole.ChiefConstructor => 0.10,
            _ => 1.0 // По умолчанию как Constructor
        };
    }

    /// <summary>
    /// Возвращает локализованное название роли.
    /// </summary>
    /// <param name="role">Роль ресурса.</param>
    /// <returns>Название на русском языке.</returns>
    public static string GetDisplayName(this ResourceRole role)
    {
        return role switch
        {
            ResourceRole.Constructor => "Конструктор",
            ResourceRole.LeadSpecialist => "Главный специалист",
            ResourceRole.ChiefConstructor => "Главный конструктор",
            _ => "Неизвестная роль"
        };
    }

    /// <summary>
    /// Проверяет, может ли роль иметь MaxWorkload меньше 100%.
    /// </summary>
    /// <param name="role">Роль ресурса.</param>
    /// <returns>True, если MaxWorkload можно редактировать.</returns>
    public static bool CanEditMaxWorkload(this ResourceRole role)
    {
        // Конструктор всегда работает на 100%
        return role != ResourceRole.Constructor;
    }

    /// <summary>
    /// Возвращает минимально допустимый MaxWorkload для роли.
    /// </summary>
    /// <param name="role">Роль ресурса.</param>
    /// <returns>Минимальный процент (0-100).</returns>
    public static int GetMinMaxWorkload(this ResourceRole role)
    {
        return role == ResourceRole.Constructor ? 100 : 0;
    }

    /// <summary>
    /// Парсит строку в ResourceRole (для миграции старых данных).
    /// </summary>
    /// <param name="roleString">Строковое представление роли.</param>
    /// <returns>ResourceRole или Constructor по умолчанию.</returns>
    public static ResourceRole ParseFromString(string? roleString)
    {
        if (string.IsNullOrWhiteSpace(roleString))
            return ResourceRole.Constructor;

        var normalized = roleString.Trim().ToLowerInvariant();

        return normalized switch
        {
            "конструктор" or "constructor" => ResourceRole.Constructor,
            "главный специалист" or "leadspecialist" or "lead specialist" => ResourceRole.LeadSpecialist,
            "главный конструктор" or "chiefconstructor" or "chief constructor" => ResourceRole.ChiefConstructor,
            _ => ResourceRole.Constructor
        };
    }
}