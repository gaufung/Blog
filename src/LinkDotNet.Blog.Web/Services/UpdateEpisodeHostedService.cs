using Amazon.Runtime.Internal.Util;

using LinkDotNet.Blog.Domain;
using LinkDotNet.Blog.Infrastructure.Persistence;
using LinkDotNet.Blog.Web.Options;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SharpCompress.Common;

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

public class UpdateEpisodeHostedService : IHostedService, IDisposable
{
    private readonly ILogger<UpdateEpisodeHostedService> logger;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IRepository<BlogPost> repository;

    private PeriodicTimer timer;

    private Task timerTask;

    private readonly EpisodeSyncOption episodeSyncOption;

    private static readonly JsonSerializerOptions GitHubContentJsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true, 
    };

    private static readonly string[] Tags = new string[] { ".NET Weekly" };

    public UpdateEpisodeHostedService(
        IHttpClientFactory httpClientFactory,
        IRepository<BlogPost> repository,
        IOptions<EpisodeSyncOption> episodeSyncOptionAccessor,
        ILogger<UpdateEpisodeHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(episodeSyncOptionAccessor);
        this.httpClientFactory = httpClientFactory;
        episodeSyncOption = episodeSyncOptionAccessor.Value;
        this.logger = logger;
        this.repository = repository;
    }

    public void Dispose() => timer?.Dispose();
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (episodeSyncOption.Enabled && !string.IsNullOrWhiteSpace(episodeSyncOption.ContentAPI))
        {
            logger.StartUpdatingEpisode();
            timer = new PeriodicTimer(TimeSpan.FromHours(1));
            timerTask = Start(cancellationToken);
        }

        return Task.CompletedTask;
    }

    private async Task Start(CancellationToken token)
    {
        if (timer is null)
        {
            return;
        }
        try
        {
            while (await timer.WaitForNextTickAsync(token) &&
                !token.IsCancellationRequested)
            {
                await Update(token);
            }
        }
        catch (OperationCanceledException)
        {
            logger.UpdateEpisodeCanceled();
        }
    }

    private async Task Update(CancellationToken token)
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
            files = files.Where(p => p.Name.StartsWith("episode", StringComparison.OrdinalIgnoreCase)).ToArray();
            var blogPosts = await repository.GetAllAsync(blogPost => blogPost.Title.StartsWith(".NET 周刊第"));
            await UpdateEpisodes(files, blogPosts, token);
        }
        else
        {
            logger.CouldNotFindGitHubEpisode(episodeSyncOption.ContentAPI, httpResponseMessage.StatusCode);
        }
    }

    private async Task UpdateEpisodes(GitHubFile[] files, IReadOnlyList<BlogPost> blogPosts, CancellationToken token)
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

                    // TODO Upgrade the BlogPost
                }
                else
                {
                    // TODO Extract the preview url
                    await repository.StoreAsync(BlogPost.Create($".NET 周刊第 {file.Id} 期", $".NET 周刊第 {file.Id} 期", fileContent.Content, "", true, null, null, Tags));
                }
            }
            else
            {
                logger.CouldNotFindGitHubEpisode(file.Url, httpResponseMessage.StatusCode);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => throw new NotImplementedException();

    class GitHubFile
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
        public string Url { get; set; }
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

    class GitHubFileContent
    {
        public string Content { get; set; }
    }
}
