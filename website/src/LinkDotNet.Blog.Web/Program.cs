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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.AzureAppServices;
using Microsoft.Extensions.Options;

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
        _ = builder.Services.AddRazorPages()
                   .Services.AddServerSideBlazor()
                   .Services.AddSignalR(options =>
                    {
                        options.MaximumReceiveMessageSize = 1024 * 1024;
                    })
                   .Services.Configure<FormOptions>(options =>
                    {
                        options.KeyLengthLimit = 1024 * 100;
                    });

        builder.Services.AddConfiguration();
        _ = builder.Services.AddHttpClient("GitHub", (sp, client) =>
        {
            var githubAuthOptions = sp.GetRequiredService<IOptions<GithubAuthOptions>>();
            if (!string.IsNullOrWhiteSpace(githubAuthOptions.Value.PAT))
            {
                client.DefaultRequestHeaders.Add("Authorization", $"token {githubAuthOptions.Value.PAT}");
            }

            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.v3+json");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("fungkao");
        });

        _ = builder.Services.AddBlazoredToast()
            .RegisterServices()
            .AddStorageProvider()
            .Configure<EpisodeSyncOptions>(builder.Configuration.GetSection("EpisodeSync"))
            .Configure<BlogSyncOptions>(builder.Configuration.GetSection("BlogSync"))
            .Configure<GithubAuthOptions>(builder.Configuration.GetSection("GithubAuth"))
            .AddResponseCompression()
            .AddHostedService<BlogPostPublisher>()
            .AddHostedService<TransformBlogPostRecordsService>()
            .AddHostedService<UpdateEpisodeService>()
            .AddHostedService<UpdateBlogService>()
            .AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("Database");

        _ = builder.Services.UseAuthentication();
        _ = builder.Logging.AddAzureWebAppDiagnostics();
        _ = builder.Services.Configure<AzureBlobLoggerOptions>(options =>
        {
            options.BlobName = "log.txt";
        });
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

        _ = app.UseResponseCompression()
            .UseHttpsRedirection()
            .UseStaticFiles();

        _ = app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
        });

        _ = app.UseRouting()
            .UseCookiePolicy()
            .UseAuthentication()
            .UseAuthorization();

        _ = app.MapControllers();
        _ = app.MapBlazorHub();
        _ = app.MapFallbackToPage("/_Host");
        _ = app.MapFallbackToPage("/searchByTag/{tag}", "/_Host");
        _ = app.MapFallbackToPage("/search/{searchTerm}", "/_Host");
    }
}
