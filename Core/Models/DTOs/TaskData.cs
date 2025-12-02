namespace Core.Models.DTOs;

[Serializable]
public class TaskData
{
    public Guid Id { get; set; }  // Id
    public string Name { get; set; }
    public string Start { get; set; }  // string для TimeSpan (как ранее)
    public string End { get; set; }
    public string Duration { get; set; }
    public float Complete { get; set; }
    public bool IsCollapsed { get; set; }
    
    public string? Deadline { get; set; }
    public string? Note { get; set; }
}