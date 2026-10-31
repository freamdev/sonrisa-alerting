using Alerts.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alerts.Api.Infrastructure.Identity;

internal sealed class TopicConfiguration : IEntityTypeConfiguration<Topic>
{
    public void Configure(EntityTypeBuilder<Topic> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Code).HasMaxLength(64).IsRequired();
        entity.HasIndex(x => x.Code).IsUnique();
        entity.Property(x => x.DisplayName).HasMaxLength(128).IsRequired();
        entity.HasData(
            new Topic { Id = 1, Code = "finance", DisplayName = "Finance" },
            new Topic { Id = 2, Code = "politics", DisplayName = "Politics" },
            new Topic { Id = 3, Code = "weather", DisplayName = "Weather" });
    }
}

internal sealed class ChannelConfiguration : IEntityTypeConfiguration<Channel>
{
    public void Configure(EntityTypeBuilder<Channel> entity)
    {
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Code).HasMaxLength(64).IsRequired();
        entity.HasIndex(x => x.Code).IsUnique();
        entity.Property(x => x.DisplayName).HasMaxLength(128).IsRequired();
        entity.HasData(
            new Channel { Id = 1, Code = "email", DisplayName = "Email" },
            new Channel { Id = 2, Code = "slack", DisplayName = "Slack" });
    }
}
