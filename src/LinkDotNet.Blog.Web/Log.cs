using Microsoft.Extensions.Logging;

using System.Net;

using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace LinkDotNet.Blog.Web;

public static partial class Log
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Error,
        Message = "Failed to fetch the episodes. Request URL: {ContentApi}, response status code {StatusCode}.")]
    public static partial void CouldNotFindGitHubEpisode(this ILogger logger, string contentApi, HttpStatusCode statusCode);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Warning,
        Message = "Episode update work has been cancelled.")]
    public static partial void UpdateEpisodeCanceled(this ILogger logger);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Updating episodes")]
    public static partial void StartUpdatingEpisode(this ILogger logger);

    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = ("Episode documents are empty. Request URL: {ContentAPI}"))]
    public static partial void FindEmptyEpisodeDocument(this ILogger logger, string contentApi);

    [LoggerMessage(0, LogLevel.Information, "Find the episode {FileName}")]
    public static partial void FindTheEpisodeDocument(this ILogger logger, string fileName);
}
