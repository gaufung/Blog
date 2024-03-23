using LinkDotNet.Blog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinkDotNet.Blog.Infrastructure.Persistence.Sql.Mapping;

internal sealed class ProfileInformationEntryConfiguration : IEntityTypeConfiguration<ProfileInformationEntry>
{
    public void Configure(EntityTypeBuilder<ProfileInformationEntry> builder)
    {
        _ = builder.HasKey(c => c.Id);
        _ = builder.Property(c => c.Id)
            .IsUnicode(false)
            .ValueGeneratedOnAdd();
        _ = builder.Property(c => c.Content).HasMaxLength(512).IsRequired();
        _ = builder.Property(c => c.SortOrder).IsRequired();
    }
}
