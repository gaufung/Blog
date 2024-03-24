using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LinkDotNet.Blog.Domain;
using LinkDotNet.Blog.Infrastructure.Persistence;
using LinkDotNet.Blog.Web.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LinkDotNet.Blog.Web.Features;

internal class UpdateBlogService(
    IHttpClientFactory httpClientFactory,
    IServiceScopeFactory serviceScopeFactory,
    IOptions<BlogSyncOptions> blogSyncOptions
) : BackgroundService
{

    private static readonly JsonSerializerOptions GitHubContentJsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new System.Threading.PeriodicTimer(TimeSpan.FromHours(6));

        while (!stoppingToken.IsCancellationRequested && blogSyncOptions.Value.Enabled)
        {
            await Start(stoppingToken);
            _ = await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private async Task Start(CancellationToken token)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<BlogPost>>();
        await Update(repository, token);
    }

    private async Task Update(IRepository<BlogPost> repository, CancellationToken token)
    {
        var httpClient = httpClientFactory.CreateClient("GitHub");
        var httpResponseMessage = await httpClient.GetAsync(new Uri(blogSyncOptions.Value.ContentAPI), token);
        if (!httpResponseMessage.IsSuccessStatusCode)
        {
            return;
        }

        using var contentStream = await httpResponseMessage.Content.ReadAsStreamAsync(token);
        var blogs = await JsonSerializer.DeserializeAsync<GitHubFile[]>(contentStream, GitHubContentJsonSerializerOptions, token);
        if (blogs is null or [])
        {
            return;
        }

        foreach (var blog in blogs)
        {
            await UpdateBlogPost(repository, blog, token);
        }
    }

    private async Task UpdateBlogPost(IRepository<BlogPost> repository, GitHubFile blog, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var httpClient = httpClientFactory.CreateClient("GitHub");
        var httpResponseMessage = await httpClient.GetAsync(new Uri(blog.Url), token);
        if (!httpResponseMessage.IsSuccessStatusCode)
        {
            return;
        }

        using var contentStream = await httpResponseMessage.Content.ReadAsStreamAsync(token);
        var fileContent = await JsonSerializer.DeserializeAsync<GitHubFileContent>(contentStream, GitHubContentJsonSerializerOptions, token);
        if (fileContent is null || string.IsNullOrWhiteSpace(fileContent.Content))
        {
            return;
        }
        fileContent.Content = Encoding.UTF8.GetString(Convert.FromBase64String(fileContent.Content));
        var blogMetadata = ParseBlogMetadata(fileContent.Content);
        if (blogMetadata.Status.Equals("published", StringComparison.OrdinalIgnoreCase))
        {
            var existingBlogPosts = await repository.GetAllAsync(blogPost => blogPost.Title == blogMetadata.Title);
            if (existingBlogPosts is null or [])
            {
                await repository.StoreAsync(BlogPost.Create(blogMetadata.Title, blogMetadata.Title, fileContent.Content, blogMetadata.Image, true, null, null, blogMetadata.Tags));
            }
            else
            {
                var existingBlogPost = existingBlogPosts[0];
                if (existingBlogPost.Content != fileContent.Content)
                {
                    await repository.DeleteAsync(existingBlogPost.Id);
                    await repository.StoreAsync(BlogPost.Create(blogMetadata.Title, blogMetadata.Title, fileContent.Content, blogMetadata.Image, true, null, null, blogMetadata.Tags));
                }
            }
        }
    }

    private static BlogMetadata ParseBlogMetadata(string content)
    {
        var lines = content.Split(Environment.NewLine);
        int? start = null;
        int? end = null;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("<!--", StringComparison.OrdinalIgnoreCase))
            {
                start = i;
            }
            if (lines[i].StartsWith("-->", StringComparison.OrdinalIgnoreCase))
            {
                end = i;
                break;
            }
        }
        if (!start.HasValue || !end.HasValue)
        {
            throw new InvalidOperationException("Could not find metadata in blog");
        }

        var metadata = string.Join(Environment.NewLine, lines.Skip(start.Value + 1).Take(end.Value - start.Value - 1));
        return JsonSerializer.Deserialize<BlogMetadata>(metadata);
    }



    private sealed class GitHubFile
    {
#pragma warning disable S1144 // Unused private types or members should be removed
#pragma warning disable S3459 // Unassigned members should be removed
        public string Name { get; set; }
        public string Url { get; set; }
#pragma warning restore S3459 // Unassigned members should be removed
#pragma warning restore S1144 // Unused private types or members should be removed

    }

    private sealed class GitHubFileContent
    {
        public string Content { get; set; }
    }

    private sealed record BlogMetadata(string Title, string[] Tags, string Status, string Image);
}