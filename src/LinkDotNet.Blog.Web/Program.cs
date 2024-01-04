using Blazored.Toast;

using HealthChecks.UI.Client;

using LinkDotNet.Blog.Web.Authentication.OpenIdConnect;
using LinkDotNet.Blog.Web.Features;
using LinkDotNet.Blog.Web.Options;
using LinkDotNet.Blog.Web.RegistrationExtensions;
using LinkDotNet.Blog.Web.Services;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using static Raven.Client.Constants;

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
        builder.Services.AddRazorPages();
        builder.Services.AddServerSideBlazor();
        builder.Services.AddSignalR(options =>
        {
            options.MaximumReceiveMessageSize = 1024 * 1024;
        });

        builder.Services.Configure<FormOptions>(options =>
        {
            options.KeyLengthLimit = 1024 * 100;
        });

        builder.Services.AddConfiguration();
        builder.Services.AddHttpClient("GitHub", client =>
        {
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.v3+json");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("dotnetweekly");
        });

        builder.Services.AddBlazoredToast();
        builder.Services.RegisterServices();
        builder.Services.AddStorageProvider(builder.Configuration);
        builder.Services.Configure<EpisodeSyncOption>(builder.Configuration.GetSection("EpisodeSync"));
        builder.Services.AddResponseCompression();
        builder.Services.AddHostedService<BlogPostPublisher>();
        builder.Services.AddHostedService<TransformBlogPostRecordsService>();
        builder.Services.AddHostedService<UpdateEpisodeHostedService>();

        builder.Services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("Database");

        builder.Services.UseAuthentication();
    }

    private static void ConfigureApp(WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseResponseCompression();
        app.UseHttpsRedirection();
        app.UseStaticFiles();

        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
        });

        app.UseRouting();

        app.UseCookiePolicy();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.MapBlazorHub();
        app.MapFallbackToPage("/_Host");
        app.MapFallbackToPage("/searchByTag/{tag}", "/_Host");
        app.MapFallbackToPage("/search/{searchTerm}", "/_Host");
    }
}
