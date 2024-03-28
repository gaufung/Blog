using LinkDotNet.Blog.Domain;
using LinkDotNet.Blog.Infrastructure.Persistence;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace LinkDotNet.Blog.Web.RegistrationExtensions;

public static class StorageProviderExtensions
{
    public static IServiceCollection AddStorageProvider(this IServiceCollection services) =>
        services.AddMemoryCache()
                .UseSqlAsStorageProvider()
                .RegisterCachedRepository<Infrastructure.Persistence.Sql.Repository<BlogPost>>();

    private static IServiceCollection RegisterCachedRepository<TRepo>(this IServiceCollection services)
        where TRepo : class, IRepository<BlogPost> =>
        services.AddScoped<TRepo>()
        .AddScoped<IRepository<BlogPost>>(provider => new CachedRepository<BlogPost>(
                provider.GetRequiredService<TRepo>(),
                provider.GetRequiredService<IMemoryCache>()));
}
