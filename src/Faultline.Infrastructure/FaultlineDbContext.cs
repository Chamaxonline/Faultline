using Faultline.Domain;
using Microsoft.EntityFrameworkCore;

namespace Faultline.Infrastructure;

public class FaultlineDbContext(DbContextOptions<FaultlineDbContext> options) : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<User> Users => Set<User>();
    public DbSet<IssueComment> IssueComments => Set<IssueComment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Organization>(e =>
        {
            e.HasIndex(o => o.Name).IsUnique();
        });

        modelBuilder.Entity<Project>(e =>
        {
            e.HasIndex(p => p.PublicKey).IsUnique();
            e.HasIndex(p => new { p.OrganizationId, p.Slug }).IsUnique();
            e.HasOne(p => p.Organization)
                .WithMany(o => o.Projects)
                .HasForeignKey(p => p.OrganizationId);
        });

        modelBuilder.Entity<Issue>(e =>
        {
            e.HasIndex(i => new { i.ProjectId, i.Fingerprint }).IsUnique();
            e.HasOne(i => i.Project)
                .WithMany(p => p.Issues)
                .HasForeignKey(i => i.ProjectId);
            // deleting a user unassigns their issues rather than cascading into issue deletion
            e.HasOne(i => i.AssignedToUser)
                .WithMany()
                .HasForeignKey(i => i.AssignedToUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Event>(e =>
        {
            e.HasIndex(ev => ev.Timestamp);
            e.HasOne(ev => ev.Issue)
                .WithMany(i => i.Events)
                .HasForeignKey(ev => ev.IssueId);
        });

        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.HasOne(u => u.Organization)
                .WithMany(o => o.Users)
                .HasForeignKey(u => u.OrganizationId);
        });

        modelBuilder.Entity<IssueComment>(e =>
        {
            e.HasIndex(c => new { c.IssueId, c.CreatedAt });
            e.HasOne(c => c.Issue)
                .WithMany()
                .HasForeignKey(c => c.IssueId)
                .OnDelete(DeleteBehavior.Cascade);
            // deleting a user keeps their past comments, just anonymizes authorship
            e.HasOne(c => c.AuthorUser)
                .WithMany()
                .HasForeignKey(c => c.AuthorUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
