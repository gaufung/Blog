using LinkDotNet.Blog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinkDotNet.Blog.Infrastructure.Persistence.Sql.Mapping;

internal sealed class TalkConfiguration : IEntityTypeConfiguration<Talk>
{
    public void Configure(EntityTypeBuilder<Talk> builder)
    {
        _ = builder.HasKey(t => t.Id);
        _ = builder.Property(t => t.Id)
            .IsUnicode(false)
            .ValueGeneratedOnAdd();
        _ = builder.Property(t => t.PresentationTitle).HasMaxLength(256).IsRequired();
        _ = builder.Property(t => t.Place).HasMaxLength(256).IsRequired();
        _ = builder.Property(t => t.PublishedDate).IsRequired();
    }
}
