namespace Core.Models.DTOs;

[Serializable]
public class SplitTaskRelation
{
    public Guid PartId { get; set; }  // Id
    public string PartName { get; set; }  // PartName
    public string PartStart { get; set; }  // string
    public string PartEnd { get; set; }
    public string PartDuration { get; set; }
    public float PartComplete { get; set; }
    public Guid SplitTaskId { get; set; }  // Id
}