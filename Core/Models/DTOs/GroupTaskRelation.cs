namespace Core.Models.DTOs;

[Serializable]
public class GroupRelation
{
    public Guid GroupId { get; set; }  // Id
    public Guid MemberId { get; set; }  // Id
}