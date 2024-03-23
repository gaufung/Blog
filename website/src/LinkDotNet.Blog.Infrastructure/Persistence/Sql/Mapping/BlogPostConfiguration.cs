using LinkDotNet.Blog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinkDotNet.Blog.Infrastructure.Persistence.Sql.Mapping;

internal sealed class BlogPostConfiguration : IEntityTypeConfiguration<BlogPost>
{
    public void Configure(EntityTypeBuilder<BlogPost> builder)
    {
        _ = builder.HasKey(c => c.Id);
        _ = builder.Property(c => c.Id)
            .IsUnicode(false)
            .ValueGeneratedOnAdd();
        _ = builder.Property(x => x.Title).HasMaxLength(256).IsRequired();
        _ = builder.Property(x => x.PreviewImageUrl).HasMaxLength(1024).IsRequired();
        _ = builder.Property(x => x.PreviewImageUrlFallback).HasMaxLength(1024);
        _ = builder.Property(x => x.Content).IsRequired();
        _ = builder.Property(x => x.ShortDescription).IsRequired();
        _ = builder.Property(x => x.Likes).IsRequired();
        _ = builder.Property(x => x.IsPublished).IsRequired();
        _ = builder.Property(x => x.Tags).HasMaxLength(2096);

        _ = builder.HasIndex(x => new { x.IsPublished, x.UpdatedDate })
            .HasDatabaseName("IX_BlogPosts_IsPublished_UpdatedDate")
            .IsDescending(false, true);
    }
}
