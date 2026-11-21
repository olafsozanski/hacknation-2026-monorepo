using ClerkAssistant.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClerkAssistant.Api.Infrastructure.Persistence.Configurations;

public class KnowledgeDocumentConfiguration : IEntityTypeConfiguration<KnowledgeDocument>
{
    public void Configure(EntityTypeBuilder<KnowledgeDocument> builder)
    {
        builder.ToTable("knowledge_documents");
        builder.Property(x => x.Title).HasMaxLength(500);
        builder.HasOne(x => x.File).WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(x => x.Chunks).WithOne(x => x.Document).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class KnowledgeChunkConfiguration : IEntityTypeConfiguration<KnowledgeChunk>
{
    public void Configure(EntityTypeBuilder<KnowledgeChunk> builder)
    {
        builder.ToTable("knowledge_chunks");
        builder.Property(x => x.Embedding).HasColumnType($"vector({AppDbContext.EmbeddingDimensions})");
        builder.HasGeneratedTsVectorColumn(x => x.SearchVector, AppDbContext.TextSearchConfig, x => new { x.Text })
            .HasIndex(x => x.SearchVector)
            .HasMethod("gin");
        builder.HasIndex(x => x.Embedding).HasMethod("hnsw").HasOperators("vector_cosine_ops");
        builder.HasIndex(x => new { x.DocumentId, x.Index }).IsUnique();
    }
}
