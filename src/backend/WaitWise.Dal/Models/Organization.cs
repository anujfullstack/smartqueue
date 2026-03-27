namespace WaitWise.Dal.Models;

public class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public ICollection<Location> Locations { get; set; } = new List<Location>();
}
