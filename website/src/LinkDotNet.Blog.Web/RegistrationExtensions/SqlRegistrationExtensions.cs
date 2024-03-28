using LinkDotNet.Blog.Infrastructure.Persistence;
using LinkDotNet.Blog.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LinkDotNet.Blog.Web.RegistrationExtensions;

public static class SqlRegistrationExtensions
{
    public static IServiceCollection UseSqlAsStorageProvider(this IServiceCollection services)
    {
        services.AddPooledDbContextFactory<BlogDbContext>(
        (s, builder) =>
        {
            var configuration = s.GetRequiredService<IOptions<ApplicationConfiguration>>();
            var connectionString = configuration.Value.ConnectionString;
            builder.UseSqlServer(connectionString)
#if DEBUG
                .EnableDetailedErrors()
#endif
                ;
        });

        return services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
    }
}
