using Blazored.Toast;

using HealthChecks.UI.Client;

using LinkDotNet.Blog.Web.Authentication.OpenIdConnect;
using LinkDotNet.Blog.Web.Features;
using LinkDotNet.Blog.Web.Options;
using LinkDotNet.Blog.Web.RegistrationExtensions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LinkDotNet.Blog.Web;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        RegisterServices(builder);

        var app = builder.Build();
        ConfigureApp(app);

        app.Run();
    }

    private static void RegisterServices(WebApplicationBuilder builder)
    {
        _ = builder.Services.AddRazorPages();
        _ = builder.Services.AddServerSideBlazor();
        _ = builder.Services.AddSignalR(options =>
        {
            options.MaximumReceiveMessageSize = 1024 * 1024;
        });

        _ = builder.Services.Configure<FormOptions>(options =>
        {
            options.KeyLengthLimit = 1024 * 100;
        });

        builder.Services.AddConfiguration();
        _ = builder.Services.AddHttpClient("GitHub", client =>
        {
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.v3+json");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("fungkao");
        });

        _ = builder.Services.AddBlazoredToast();
        builder.Services.RegisterServices();
        builder.Services.AddStorageProvider(builder.Configuration);
        _ = builder.Services.Configure<EpisodeSyncOptions>(builder.Configuration.GetSection("EpisodeSync"));
        _ = builder.Services.Configure<BlogSyncOptions>(builder.Configuration.GetSection("BlogSync"));
        _ = builder.Services.AddResponseCompression();
        _ = builder.Services.AddHostedService<BlogPostPublisher>();
        _ = builder.Services.AddHostedService<TransformBlogPostRecordsService>();
        _ = builder.Services.AddHostedService<UpdateEpisodeService>();
        _ = builder.Services.AddHostedService<UpdateBlogService>();

        _ = builder.Services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("Database");

        builder.Services.UseAuthentication();
    }

    private static void ConfigureApp(WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            _ = app.UseDeveloperExceptionPage();
        }
        else
        {
            _ = app.UseExceptionHandler("/Error");
            _ = app.UseHsts();
        }

        _ = app.UseResponseCompression();
        _ = app.UseHttpsRedirection();
        _ = app.UseStaticFiles();

        _ = app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
        });

        _ = app.UseRouting();

        _ = app.UseCookiePolicy();
        _ = app.UseAuthentication();
        _ = app.UseAuthorization();

        _ = app.MapControllers();
        _ = app.MapBlazorHub();
        _ = app.MapFallbackToPage("/_Host");
        _ = app.MapFallbackToPage("/searchByTag/{tag}", "/_Host");
        _ = app.MapFallbackToPage("/search/{searchTerm}", "/_Host");
    }
}
