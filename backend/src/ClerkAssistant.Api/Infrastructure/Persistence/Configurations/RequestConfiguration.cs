using System.Text.Json;
using System.Text.Json.Serialization;
using ClerkAssistant.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClerkAssistant.Api.Infrastructure.Persistence.Configurations;

public class InfoRequestConfiguration : IEntityTypeConfiguration<InfoRequest>
{
    public void Configure(EntityTypeBuilder<InfoRequest> builder)
    {
        builder.ToTable("requests");
        builder.Property(x => x.CaseNumber).HasMaxLength(64);
        builder.Property(x => x.Subject).HasMaxLength(500);
        builder.Property(x => x.RequesterName).HasMaxLength(300);
        builder.Property(x => x.RequesterContact).HasMaxLength(500);
        builder.Property(x => x.ExtensionReason).HasMaxLength(2000);
        builder.Property(x => x.Version).IsRowVersion();
        builder.Property(x => x.Embedding).HasColumnType($"vector({AppDbContext.EmbeddingDimensions})");
        builder.Ignore(x => x.EffectiveDueOn);
        builder.Ignore(x => x.IsClosed);

        builder.HasGeneratedTsVectorColumn(x => x.SearchVector, AppDbContext.TextSearchConfig, x => new { x.Subject, x.Content })
            .HasIndex(x => x.SearchVector)
            .HasMethod("gin");
        builder.HasIndex(x => x.Subject).HasMethod("gin").HasOperators("gin_trgm_ops");
        builder.HasIndex(x => x.Embedding).HasMethod("hnsw").HasOperators("vector_cosine_ops");
        builder.HasIndex(x => new { x.Year, x.SequenceNumber }).IsUnique();
        builder.HasIndex(x => x.CaseNumber).IsUnique();
        builder.HasIndex(x => x.ReceivedOn);

        builder.HasOne(x => x.Assignee).WithMany().HasForeignKey(x => x.AssigneeId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(x => x.Departments).WithOne().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Documents).WithOne().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Draft).WithOne().HasForeignKey<ResponseDraft>(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RequestDepartmentConfiguration : IEntityTypeConfiguration<RequestDepartment>
{
    public void Configure(EntityTypeBuilder<RequestDepartment> builder)
    {
        builder.ToTable("request_departments");
        builder.HasKey(x => new { x.RequestId, x.DepartmentId });
        builder.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class RequestDocumentConfiguration : IEntityTypeConfiguration<RequestDocument>
{
    public void Configure(EntityTypeBuilder<RequestDocument> builder)
    {
        builder.ToTable("request_documents");
        builder.HasOne(x => x.File).WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class ResponseDraftConfiguration : IEntityTypeConfiguration<ResponseDraft>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public void Configure(EntityTypeBuilder<ResponseDraft> builder)
    {
        builder.ToTable("response_drafts");
        builder.Property(x => x.Version).IsRowVersion();
        builder.Property(x => x.Redactions)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<Redaction>>(v, JsonOptions) ?? new List<Redaction>(),
                new ValueComparer<List<Redaction>>(
                    (a, b) => a!.SequenceEqual(b!),
                    v => v.Aggregate(0, (hash, r) => HashCode.Combine(hash, r.GetHashCode())),
                    v => v.ToList()));
    }
}
