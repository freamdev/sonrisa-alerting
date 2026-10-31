using Alerts.Api.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Alerts.Api.Infrastructure.Identity;

public sealed class AlertsIdentityDbContext(DbContextOptions<AlertsIdentityDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<Channel> Channels => Set<Channel>();
    public DbSet<UserTopicSelection> UserTopicSelections => Set<UserTopicSelection>();
    public DbSet<UserChannelSelection> UserChannelSelections => Set<UserChannelSelection>();
    public DbSet<AlertEvent> AlertEvents => Set<AlertEvent>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfiguration(new TopicConfiguration());
        builder.ApplyConfiguration(new ChannelConfiguration());
        builder.Entity<UserTopicSelection>(entity =>
        {
            entity.HasKey(x => new { x.UserId, x.TopicId });
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Topic>().WithMany().HasForeignKey(x => x.TopicId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<UserChannelSelection>(entity =>
        {
            entity.HasKey(x => new { x.UserId, x.ChannelId });
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Channel>().WithMany().HasForeignKey(x => x.ChannelId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<AlertEvent>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExternalEventId).HasMaxLength(256).IsRequired();
            entity.HasIndex(x => x.ExternalEventId).IsUnique();
            entity.Property(x => x.Title).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Body).IsRequired();
            entity.HasOne<Topic>().WithMany().HasForeignKey(x => x.TopicId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<Delivery>(entity =>
        {
            entity.ToTable("Deliveries", table =>
            {
                table.HasCheckConstraint("CK_Deliveries_AttemptCount", "\"AttemptCount\" BETWEEN 0 AND 3");
                table.HasCheckConstraint("CK_Deliveries_Status", "\"Status\" IN ('Pending', 'Succeeded', 'Failed')");
            });
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.EventId, x.UserId, x.ChannelId }).IsUnique();
            entity.HasIndex(x => new { x.Status, x.AttemptCount });
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.LastError).HasMaxLength(512);
            entity.HasOne<AlertEvent>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Channel>().WithMany().HasForeignKey(x => x.ChannelId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
