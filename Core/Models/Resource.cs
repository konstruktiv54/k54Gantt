using Core.Models.DTOs;

namespace Core.Models;

/// <summary>
/// Представляет ресурс проекта (исполнитель, оборудование и т.д.).
/// </summary>
[Serializable]
public class Resource
{
    /// <summary>
    /// Уникальный идентификатор ресурса.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Имя ресурса (ФИО исполнителя или название оборудования).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Инициалы для отображения на диаграмме (например, "ИИ" для Иван Иванов).
    /// </summary>
    public string Initials { get; set; } = string.Empty;

    /// <summary>
    /// Цвет ресурса в формате HEX (например, "#4682B4").
    /// Используется для визуального выделения на диаграмме.
    /// </summary>
    public string ColorHex { get; set; } = "#4682B4"; // SteelBlue

    /// <summary>
    /// Роль или должность ресурса.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Максимальная загрузка ресурса в процентах (по умолчанию 100%).
    /// </summary>
    public int MaxWorkload { get; set; } = 100;

    /// <summary>
    /// Дата создания записи о ресурсе.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Генерирует инициалы из имени автоматически.
    /// </summary>
    public void GenerateInitialsFromName()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            Initials = "??";
            return;
        }

        var parts = Name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            // Берём первые буквы первых двух слов
            Initials = $"{parts[0][0]}{parts[1][0]}".ToUpper();
        }
        else if (parts.Length == 1 && parts[0].Length >= 2)
        {
            // Берём первые две буквы единственного слова
            Initials = parts[0].Substring(0, 2).ToUpper();
        }
        else
        {
            Initials = Name.Length > 0 ? Name[0].ToString().ToUpper() : "?";
        }
    }

    /// <summary>
    /// Преобразует в ResourceData для сериализации.
    /// </summary>
    public ResourceData ToData()
    {
        return new ResourceData
        {
            Id = this.Id,
            Name = this.Name,
            Initials = this.Initials,
            Color = this.ColorHex,
            Role = this.Role,
            MaxWorkload = this.MaxWorkload
        };
    }

    /// <summary>
    /// Создаёт Resource из ResourceData.
    /// </summary>
    public static Resource FromData(ResourceData data)
    {
        if (data == null) return null;

        return new Resource
        {
            Id = data.Id,
            Name = data.Name ?? string.Empty,
            Initials = data.Initials ?? string.Empty,
            ColorHex = data.Color ?? "#4682B4",
            Role = data.Role ?? string.Empty,
            MaxWorkload = data.MaxWorkload > 0 ? data.MaxWorkload : 100
        };
    }

    public override string ToString()
    {
        return $"{Name} ({Initials})";
    }

    public override bool Equals(object obj)
    {
        if (obj is Resource other)
            return Id == other.Id;
        return false;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}