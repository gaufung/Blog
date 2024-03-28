using LinkDotNet.Blog.Web.Features.Admin.BlogPostEditor.Services;
using LinkDotNet.Blog.Web.Features.Admin.Sitemap.Services;
using LinkDotNet.Blog.Web.Features.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LinkDotNet.Blog.Web;

public static class ServiceExtensions
{
    public static IServiceCollection RegisterServices(this IServiceCollection services) =>
        services.AddScoped<ILocalStorageService, LocalStorageService>()
                .AddScoped<ISortOrderCalculator, SortOrderCalculator>()
                .AddScoped<IUserRecordService, UserRecordService>()
                .AddScoped<ISitemapService, SitemapService>()
                .AddScoped<IXmlFileWriter, XmlFileWriter>()
                .AddScoped<IFileProcessor, FileProcessor>();
}
