using System;
using System.Threading.Tasks;
using LinkDotNet.Blog.Domain;
using LinkDotNet.Blog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;

namespace LinkDotNet.Blog.Web.Features.Services;

public sealed partial class UserRecordService(
    IRepository<UserRecord> userRecordRepository,
    NavigationManager navigationManager,
    AuthenticationStateProvider authenticationStateProvider,
    ILogger<UserRecordService> logger) : IUserRecordService
{
    private readonly IRepository<UserRecord> userRecordRepository = userRecordRepository;
    private readonly NavigationManager navigationManager = navigationManager;
    private readonly AuthenticationStateProvider authenticationStateProvider = authenticationStateProvider;
    private readonly ILogger<UserRecordService> logger = logger;

    public async ValueTask StoreUserRecordAsync()
    {
        try
        {
            await GetAndStoreUserRecordAsync();
        }
        catch (Exception e)
        {
            LogUserRecordError(e);
        }
    }

    private async ValueTask GetAndStoreUserRecordAsync()
    {
        var userIdentity = (await authenticationStateProvider.GetAuthenticationStateAsync()).User.Identity;
        if (userIdentity == null || userIdentity.IsAuthenticated)
        {
            return;
        }

        var url = GetClickedUrl();

        var record = new UserRecord
        {
            DateClicked = DateOnly.FromDateTime(DateTime.UtcNow),
            UrlClicked = url,
        };

        await userRecordRepository.StoreAsync(record);
    }

    private string GetClickedUrl()
    {
        var basePath = navigationManager.ToBaseRelativePath(navigationManager.Uri);

        if (string.IsNullOrEmpty(basePath))
        {
            return string.Empty;
        }

        var queryIndex = basePath.IndexOf('?', StringComparison.OrdinalIgnoreCase);
        return queryIndex >= 0 ? basePath[..queryIndex] : basePath;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Error while storing user record service.")]
    private partial void LogUserRecordError(Exception exception);
}
