using LinkDotNet.Blog.Domain;
using LinkDotNet.Blog.Infrastructure.Persistence;
using LinkDotNet.Blog.Web.Options;

using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LinkDotNet.Blog.Web.Services;

public sealed class UpdateEpisodeHostedService : BackgroundService
{
    private readonly ILogger<UpdateEpisodeHostedService> logger;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IServiceProvider serviceProvider;

    private readonly EpisodeSyncOption episodeSyncOption;

    private static readonly JsonSerializerOptions GitHubContentJsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true, 
    };

    private static readonly string[] Tags = [".NET Weekly"];

    public UpdateEpisodeHostedService(
        IHttpClientFactory httpClientFactory,
        IServiceProvider serviceProvider,
        IOptions<EpisodeSyncOption> episodeSyncOptionAccessor,
        ILogger<UpdateEpisodeHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(episodeSyncOptionAccessor);
        this.httpClientFactory = httpClientFactory;
        episodeSyncOption = episodeSyncOptionAccessor.Value;
        this.logger = logger;
        this.serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        while (!stoppingToken.IsCancellationRequested)
        {
            await Start(stoppingToken);

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private async Task Start(CancellationToken token)
    {
        using var scope = serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<BlogPost>>();
        await Update(repository, token);
    }

    private async Task Update(IRepository<BlogPost> repository, CancellationToken token)
    {
        var httpClient = httpClientFactory.CreateClient("GitHub");
        var httpResponseMessage = await httpClient.GetAsync(new Uri(episodeSyncOption.ContentAPI), token);
        if (httpResponseMessage.IsSuccessStatusCode)
        {
            using var contentStream = await httpResponseMessage.Content.ReadAsStreamAsync(token);
            var files = await JsonSerializer.DeserializeAsync<GitHubFile[]>(contentStream, GitHubContentJsonSerializerOptions, cancellationToken: token);
            if (files == null || files.Length == 0)
            {
                logger.FindEmptyEpisodeDocument(episodeSyncOption.ContentAPI);
                return;
            }
            files = files.Where(p => p.Name.StartsWith("episode", StringComparison.OrdinalIgnoreCase)).OrderBy(p => p.Id).ToArray();
            var blogPosts = await repository.GetAllAsync(blogPost => blogPost.Title.StartsWith(".NET 周刊第"));
            await UpdateEpisodes(repository, files, blogPosts, token);
        }
        else
        {
            logger.CouldNotFindGitHubEpisode(episodeSyncOption.ContentAPI, httpResponseMessage.StatusCode);
        }
    }

    private async Task UpdateEpisodes(IRepository<BlogPost> repository, GitHubFile[] files, IReadOnlyList<BlogPost> blogPosts, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(blogPosts);

        foreach (var file in files)
        {
            token.ThrowIfCancellationRequested();
            var httpClient = httpClientFactory.CreateClient("GitHub");
            var httpResponseMessage = await httpClient.GetAsync(new Uri(file.Url), token);
            if (httpResponseMessage.IsSuccessStatusCode)
            {
                logger.FindEmptyEpisodeDocument(file.Name);
                using var contentStream = await httpResponseMessage.Content.ReadAsStreamAsync(token);
                var fileContent = await JsonSerializer.DeserializeAsync<GitHubFileContent>(contentStream, GitHubContentJsonSerializerOptions, token);

                if (fileContent == null || string.IsNullOrWhiteSpace(fileContent.Content))
                {
                    continue;
                }

                fileContent.Content = Encoding.UTF8.GetString(Convert.FromBase64String(fileContent.Content));
                var blogPost = blogPosts.FirstOrDefault(p => p.Title == $".NET 周刊第 {file.Id} 期");
                if (blogPost != null)
                {
                    if (fileContent.Content == blogPost.Content)
                    {
                        continue;
                    }

                    await repository.DeleteAsync(blogPost.Id);
                    var previewImageUrl = ExtractFirstImageLinkFromMarkdown(fileContent.Content);
                    await repository.StoreAsync(BlogPost.Create($".NET 周刊第 {file.Id} 期", $".NET 周刊第 {file.Id} 期", fileContent.Content, previewImageUrl, true, blogPost.UpdatedDate, null, Tags));
                }
                else
                {
                    var previewImageUrl = ExtractFirstImageLinkFromMarkdown(fileContent.Content);
                    await repository.StoreAsync(BlogPost.Create($".NET 周刊第 {file.Id} 期", $".NET 周刊第 {file.Id} 期", fileContent.Content, previewImageUrl, true, null, null, Tags));
                }
            }
            else
            {
                logger.CouldNotFindGitHubEpisode(file.Url, httpResponseMessage.StatusCode);
            }
        }
    }

    private static string ExtractFirstImageLinkFromMarkdown(string markdownContent)
    {
        var doc = Markdown.Parse(markdownContent);
        // Select paragraph blocks and then all LinkInline and take the first one
        var link = doc.Descendants<ParagraphBlock>().SelectMany(x => x.Inline.Descendants<LinkInline>()).FirstOrDefault(l => l.IsImage);
        return link?.Url ?? string.Empty;
    }

    sealed class GitHubFile
    {
        private static readonly Regex Regex = new(@"episode-(?<index>\d+)\.md");

#pragma warning disable IDE1006 // Naming Styles
        private string _name;
#pragma warning restore IDE1006 // Naming Styles
        public string Name
        {
            get => _name ?? string.Empty;
#pragma warning disable S1144 // Unused private types or members should be removed
            set
#pragma warning restore S1144 // Unused private types or members should be removed
            {
                _name = value;
                ParseEpisode(value);
            }
        }

#pragma warning disable S1144 // Unused private types or members should be removed
#pragma warning disable S3459 // Unassigned members should be removed
        public string Url { get; set; }
#pragma warning restore S3459 // Unassigned members should be removed
#pragma warning restore S1144 // Unused private types or members should be removed

        public string Id { get; private set; }

        public string Title { get; private set; }

        private void ParseEpisode(string name)
        {
            var match = Regex.Match(name);
            if (match.Success)
            {
#pragma warning disable CA1305 // Specify IFormatProvider
                var index = int.Parse(match.Groups["index"].Value);
#pragma warning restore CA1305 // Specify IFormatProvider
                Id = $"{index}";
                Title = $".NET 周刊第 {index} 期";
            }
        }

    }

    sealed class GitHubFileContent
    {
        public string Content { get; set; }
    }
}
