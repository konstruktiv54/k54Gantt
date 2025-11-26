using Core.Models.DTOs;

namespace Core.Models;

/// <summary>
/// Представляет назначение ресурса на задачу.
/// Реализует связь Many-to-Many между Resource и Task.
/// </summary>
[Serializable]
public class ResourceAssignment
{
    /// <summary>
    /// Уникальный идентификатор назначения.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Идентификатор задачи, на которую назначен ресурс.
    /// </summary>
    public Guid TaskId { get; set; }

    /// <summary>
    /// Идентификатор назначенного ресурса.
    /// </summary>
    public Guid ResourceId { get; set; }

    /// <summary>
    /// Процент загрузки ресурса на данной задаче (0-100).
    /// Зарезервировано для будущего использования.
    /// По умолчанию 100%.
    /// </summary>
    public int Workload { get; set; } = 100;

    /// <summary>
    /// Дополнительные заметки о назначении.
    /// </summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// Дата и время создания назначения.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Проверяет, соответствует ли назначение указанной паре задача-ресурс.
    /// </summary>
    public bool Matches(Guid taskId, Guid resourceId)
    {
        return TaskId == taskId && ResourceId == resourceId;
    }

    /// <summary>
    /// Возвращает строковое представление назначения.
    /// </summary>
    public override string ToString()
    {
        return $"Assignment: Task={TaskId}, Resource={ResourceId}, Workload={Workload}%";
    }

    /// <summary>
    /// Проверяет равенство по идентификатору.
    /// </summary>
    public override bool Equals(object obj)
    {
        if (obj is ResourceAssignment other)
        {
            return Id == other.Id;
        }
        return false;
    }

    /// <summary>
    /// Возвращает хэш-код.
    /// </summary>
    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    /// <summary>
    /// Преобразует в ResourceAssignmentData для сериализации.
    /// </summary>
    public ResourceAssignmentData ToData()
    {
        return new ResourceAssignmentData
        {
            Id = this.Id,
            TaskId = this.TaskId,
            ResourceId = this.ResourceId,
            Workload = this.Workload,
            Notes = this.Notes
        };
    }

    /// <summary>
    /// Создаёт ResourceAssignment из ResourceAssignmentData.
    /// </summary>
    public static ResourceAssignment FromData(ResourceAssignmentData data)
    {
        if (data == null) return null;

        return new ResourceAssignment
        {
            Id = data.Id,
            TaskId = data.TaskId,
            ResourceId = data.ResourceId,
            Workload = data.Workload > 0 ? data.Workload : 100,
            Notes = data.Notes ?? string.Empty
        };
    }
}