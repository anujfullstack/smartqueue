namespace WaitWise.Dal.Models;

public class Location
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public Organization Organization { get; set; } = null!;
    public ICollection<Queue> Queues { get; set; } = new List<Queue>();
}
