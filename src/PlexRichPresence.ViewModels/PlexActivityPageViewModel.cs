﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PlexRichPresence.PlexActivity;
using PlexRichPresence.ViewModels.Models;
using PlexRichPresence.ViewModels.Services;

namespace PlexRichPresence.ViewModels;

[INotifyPropertyChanged]
public partial class PlexActivityPageViewModel
{
    private readonly IPlexActivityService plexActivityService;
    private readonly IStorageService storageService;
    private readonly IDiscordService discordService;
    private readonly INavigationService navigationService;
    private string userToken = string.Empty;
    private string userName = string.Empty;
    private ILogger<PlexActivityPageViewModel> logger;

    [ObservableProperty] private string currentActivity = "Idle";
    [ObservableProperty] private string plexServerIp = string.Empty;
    [ObservableProperty] private int plexServerPort;
    [ObservableProperty] private bool isPlexServerOwned;
    [ObservableProperty] private bool isServerUnreachable;
    [ObservableProperty] private bool enableIdleStatus = true;
    
    public PlexActivityPageViewModel(
        IPlexActivityService plexActivityService,
        IStorageService storageService,
        INavigationService navigationService,
        IDiscordService discordService,
        ILogger<PlexActivityPageViewModel> logger
        )
    {
        this.plexActivityService = plexActivityService;
        this.storageService = storageService;
        this.navigationService = navigationService;
        this.discordService = discordService;
        this.logger = logger;
    }

    [RelayCommand]
    private async Task InitStrategy()
    {
        PlexServerIp = await storageService.GetAsync("serverIp");
        PlexServerPort = int.Parse(await storageService.GetAsync("serverPort"));
        userToken = await storageService.GetAsync("plex_token");
        userName = await storageService.GetAsync("plexUserName");
        IsPlexServerOwned = bool.Parse(await storageService.GetAsync("isServerOwned"));
        if (await storageService.ContainsKeyAsync("enableIdleStatus"))
        {
            EnableIdleStatus = bool.Parse(await storageService.GetAsync("enableIdleStatus"));
        }
    }

    [RelayCommand]
    private async Task StartActivity()
    {
        try
        {
            await foreach (IPlexSession session in plexActivityService.GetSessions(
                               IsPlexServerOwned,
                               userName,
                               PlexServerIp,
                               PlexServerPort,
                               userToken)
                          )
            {
                CurrentActivity = session.MediaTitle;

                if (CurrentActivity is "Idle" && EnableIdleStatus)
                {
                    discordService.SetDiscordPresenceToPlexSession(session);
                }
                else if ((CurrentActivity is "Idle" && !EnableIdleStatus))
                {
                    discordService.StopRichPresence();
                }
                else
                {
                    discordService.SetDiscordPresenceToPlexSession(session);
                }
                
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Could not connect to server");
            IsServerUnreachable = true;
        }

    }

    [RelayCommand]
    private async Task ChangeServer()
    {
        plexActivityService.Disconnect();
        await navigationService.NavigateToAsync("servers");
    }
}