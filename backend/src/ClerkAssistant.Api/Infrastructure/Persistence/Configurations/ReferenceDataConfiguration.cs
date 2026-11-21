using ClerkAssistant.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClerkAssistant.Api.Infrastructure.Persistence.Configurations;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.Property(x => x.FullName).HasMaxLength(200);
        builder.Property(x => x.Initials).HasMaxLength(8);
    }
}

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("departments");
        builder.Property(x => x.Symbol).HasMaxLength(16);
        builder.Property(x => x.Name).HasMaxLength(300);
        builder.Property(x => x.Email).HasMaxLength(200);
        builder.HasIndex(x => x.Symbol).IsUnique();
    }
}

public class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> builder)
    {
        builder.ToTable("files");
        builder.Property(x => x.FileName).HasMaxLength(255);
        builder.Property(x => x.ContentType).HasMaxLength(128);
        builder.Property(x => x.Sha256).HasMaxLength(64);
    }
}

public class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_entries");
        builder.Property(x => x.UserName).HasMaxLength(256);
        builder.Property(x => x.Action).HasMaxLength(64);
        builder.Property(x => x.EntityType).HasMaxLength(64);
        builder.Property(x => x.Details).HasMaxLength(1000);
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
        builder.HasIndex(x => x.At);
    }
}
