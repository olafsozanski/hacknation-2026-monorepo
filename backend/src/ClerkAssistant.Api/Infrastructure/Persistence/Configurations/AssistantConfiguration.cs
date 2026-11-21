using System.Text.Json;
using System.Text.Json.Serialization;
using ClerkAssistant.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClerkAssistant.Api.Infrastructure.Persistence.Configurations;

public class AssistantConversationConfiguration : IEntityTypeConfiguration<AssistantConversation>
{
    public const int MaxTitleLength = 200;

    public void Configure(EntityTypeBuilder<AssistantConversation> builder)
    {
        builder.ToTable("assistant_conversations");
        builder.Property(x => x.Title).HasMaxLength(MaxTitleLength);
        builder.HasIndex(x => new { x.UserId, x.UpdatedAt });
        builder.HasIndex(x => x.UpdatedAt);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Messages).WithOne().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AssistantMessageConfiguration : IEntityTypeConfiguration<AssistantMessage>
{
    public void Configure(EntityTypeBuilder<AssistantMessage> builder)
    {
        builder.ToTable("assistant_messages");
        builder.Property(x => x.Sequence).UseIdentityByDefaultColumn();
        builder.HasIndex(x => new { x.ConversationId, x.Sequence });
        builder.HasMany(x => x.Actions).WithOne().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AssistantActionConfiguration : IEntityTypeConfiguration<AssistantAction>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public void Configure(EntityTypeBuilder<AssistantAction> builder)
    {
        builder.ToTable("assistant_actions");
        builder.Property(x => x.CaseNumber).HasMaxLength(64);
        builder.Property(x => x.Summary).HasMaxLength(500);
        builder.Property(x => x.Outcome).HasMaxLength(2000);
        builder.Property(x => x.Version).IsRowVersion();
        builder.Property(x => x.Details).HasColumnType("jsonb").HasConversion(
            v => JsonSerializer.Serialize(v, JsonOptions),
            v => JsonSerializer.Deserialize<List<string>>(v, JsonOptions) ?? new List<string>(),
            new ValueComparer<List<string>>(
                (a, b) => a!.SequenceEqual(b!),
                v => v.Aggregate(0, (hash, line) => HashCode.Combine(hash, line.GetHashCode())),
                v => v.ToList()));
        builder.Property(x => x.Arguments).HasColumnType("jsonb").HasConversion(
            v => JsonSerializer.Serialize(v, JsonOptions),
            v => JsonSerializer.Deserialize<AssistantActionArguments>(v, JsonOptions) ?? new AssistantActionArguments(),
            new ValueComparer<AssistantActionArguments>(
                (a, b) => JsonSerializer.Serialize(a, JsonOptions) == JsonSerializer.Serialize(b, JsonOptions),
                v => JsonSerializer.Serialize(v, JsonOptions).GetHashCode(),
                v => v));
        builder.HasOne<InfoRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
    }
}
