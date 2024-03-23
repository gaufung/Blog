using LinkDotNet.Blog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinkDotNet.Blog.Infrastructure.Persistence.Sql.Mapping;

internal sealed class UserRecordConfiguration : IEntityTypeConfiguration<UserRecord>
{
    public void Configure(EntityTypeBuilder<UserRecord> builder)
    {
        _ = builder.HasKey(s => s.Id);
        _ = builder.Property(s => s.Id)
            .IsUnicode(false)
            .ValueGeneratedOnAdd();
        _ = builder.Property(s => s.UrlClicked).HasMaxLength(256).IsRequired();
    }
}
