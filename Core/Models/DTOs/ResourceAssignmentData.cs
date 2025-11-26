namespace Core.Models.DTOs;

[Serializable]
public class ResourceAssignmentData
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid ResourceId { get; set; }
    public int Workload { get; set; }
    public string Notes { get; set; }

    public ResourceAssignmentData()
    {
        Workload = 100;
        Notes = string.Empty;
    }
}