using DiscordRPC;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using PlexRichPresence.DiscordRichPresence.Rendering;
using PlexRichPresence.ViewModels.Models;
using PlexRichPresence.ViewModels.Services;

namespace PlexRichPresence.DiscordRichPresence.Tests;

public class PlexSessionRenderingServiceTests
{
    public static TheoryData<FakePlexSession, string?, string?, bool, TimeSpan>
        RenderingTheoryData =>
        new()
        {
            {
                new FakePlexSession
                {
                    MediaTitle = "Test Movie", MediaType = PlexMediaType.Movie, PlayerState = PlexPlayerState.Buffering
                },
                "⟲\x2800", "Test Movie", true, TimeSpan.Zero
            },
            {
                new FakePlexSession
                {
                    MediaTitle = "Test Movie", MediaType = PlexMediaType.Movie, PlayerState = PlexPlayerState.Paused
                },
                "⏸\x2800" , "Test Movie", true, TimeSpan.Zero
            },
            {
                new FakePlexSession
                {
                    MediaTitle = "Test Movie", MediaType = PlexMediaType.Movie, PlayerState = PlexPlayerState.Playing,
                    Duration = 20_000, ViewOffset = 10_000
                },
                "▶\x2800", "Test Movie", true, TimeSpan.FromSeconds(10)
            },
            {
                new FakePlexSession
                {
                    MediaTitle = "Test Title", MediaParentTitle = "Test Parent Title",
                    MediaGrandParentTitle = "Test Grand Parent Title", MediaType = PlexMediaType.Unknown,
                    PlayerState = PlexPlayerState.Paused
                },
                "Test Title", "Test Grand Parent Title - Test Parent Title", true, TimeSpan.Zero
            },
            {
                new FakePlexSession
                {
                    MediaTitle = "Test Title", MediaParentTitle = "Test Parent Title",
                    MediaGrandParentTitle = "Test Grand Parent Title", MediaType = PlexMediaType.Track,
                    PlayerState = PlexPlayerState.Paused
                },
                "⏸ Test Grand Parent Title", "♫ Test Title", true, TimeSpan.Zero
            },
            {
                new FakePlexSession
                {
                    MediaTitle = "Test Title", MediaParentTitle = "Test Parent Title",
                    MediaGrandParentTitle = "Test Grand Parent Title", MediaType = PlexMediaType.Episode,
                    PlayerState = PlexPlayerState.Paused
                },
                "⏸ Test Grand Parent Title", "⏏ Test Title", true, TimeSpan.Zero
            },
            {
                new FakePlexSession
                {
                    MediaTitle = "Idle",
                    MediaParentTitle = string.Empty,
                    MediaGrandParentTitle = string.Empty,
                    MediaType = PlexMediaType.Idle,
                    PlayerState = PlexPlayerState.Idle
                },
                "Idle", null, false, TimeSpan.Zero
            }
        };


    [Theory]
    [MemberData(nameof(RenderingTheoryData))]
    public void RenderPlayerState(
        FakePlexSession session,
        string? expectedState,
        string? expectedDetail,
        bool setEndTimeStamp,
        TimeSpan expectedEndTimestampAfterNow
    )
    {
        // Given
        DateTime dateTime = DateTime.Now.ToUniversalTime();
        Mock<IClock> mockClock = SharedSetup.BuildMockClock(dateTime);
        var plexSessionRenderingService =
            new PlexSessionRenderingService(new PlexSessionRendererFactory(mockClock.Object), new Mock<ILogger<PlexSessionRenderingService>>().Object);

        // When
        RichPresence presence = plexSessionRenderingService.RenderSession(session);

        // Then
        presence.State.Should().Be(expectedState);
        presence.Details.Should().Be(expectedDetail);
        if (setEndTimeStamp)
        {
            presence.Timestamps.End.Should().Be(dateTime.Add(expectedEndTimestampAfterNow));
        }
    }
}