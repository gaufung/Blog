using LinkDotNet.Blog.Web.Features.Admin.BlogPostEditor.Services;
using LinkDotNet.Blog.Web.Features.Admin.Sitemap.Services;
using LinkDotNet.Blog.Web.Features.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LinkDotNet.Blog.Web;

public static class ServiceExtensions
{
    public static void RegisterServices(this IServiceCollection services)
    {
        _ = services.AddScoped<ILocalStorageService, LocalStorageService>();
        _ = services.AddScoped<ISortOrderCalculator, SortOrderCalculator>();
        _ = services.AddScoped<IUserRecordService, UserRecordService>();
        _ = services.AddScoped<ISitemapService, SitemapService>();
        _ = services.AddScoped<IXmlFileWriter, XmlFileWriter>();
        _ = services.AddScoped<IFileProcessor, FileProcessor>();
    }
}
