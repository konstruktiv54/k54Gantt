namespace Core.Models.DTOs;

[Serializable]
public class ResourceData
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Initials { get; set; }
    public string Color { get; set; }
    public string Role { get; set; }
    public int MaxWorkload { get; set; }

    public ResourceData()
    {
        MaxWorkload = 100;
        Color = "#4682B4";
    }
}