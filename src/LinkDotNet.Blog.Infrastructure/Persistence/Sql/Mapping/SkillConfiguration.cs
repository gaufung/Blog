using LinkDotNet.Blog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinkDotNet.Blog.Infrastructure.Persistence.Sql.Mapping;

internal sealed class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        _ = builder.HasKey(s => s.Id);
        _ = builder.Property(s => s.Id)
            .IsUnicode(false)
            .ValueGeneratedOnAdd();
        _ = builder.Property(s => s.ProficiencyLevel)
            .HasConversion(to => to.Key, from => ProficiencyLevel.Create(from))
            .HasMaxLength(32)
            .IsRequired();
        _ = builder.Property(s => s.Name).HasMaxLength(128).IsRequired();
        _ = builder.Property(s => s.IconUrl).HasMaxLength(1024);
        _ = builder.Property(s => s.Capability).HasMaxLength(128).IsRequired();
    }
}
