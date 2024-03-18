using LinkDotNet.Blog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinkDotNet.Blog.Infrastructure.Persistence.Sql.Mapping;

internal sealed class BlogPostRecordConfiguration : IEntityTypeConfiguration<BlogPostRecord>
{
    public void Configure(EntityTypeBuilder<BlogPostRecord> builder)
    {
        _ = builder.HasKey(s => s.Id);
        _ = builder.Property(s => s.Id)
            .IsUnicode(false)
            .ValueGeneratedOnAdd();
        _ = builder.Property(s => s.BlogPostId).HasMaxLength(256).IsRequired();
    }
}
