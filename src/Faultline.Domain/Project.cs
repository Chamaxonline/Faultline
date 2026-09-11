namespace Faultline.Domain;

public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string PublicKey { get; set; } = GenerateKey();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Organization Organization { get; set; } = default!;
    public List<Issue> Issues { get; set; } = [];

    private static string GenerateKey() => Guid.NewGuid().ToString("N");
}
