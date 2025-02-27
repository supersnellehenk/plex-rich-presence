using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using Moq;
using Plex.ServerApi.Clients.Interfaces;
using Plex.ServerApi.PlexModels.Media;
using Websocket.Client;
using Xunit;

namespace PlexRichPresence.PlexActivity.Tests;

public class PlexSessionWebSocketStrategyTests
{
    [Fact]
    public async Task GetSessions_GetSessionFromWebSocket()
    {
        // Given
        const string fakeToken = "test plex token";
        const string fakeServerIp = "111.111.111.111";
        const int fakeServerPort = 32400;

        Mock<IWebSocketClientFactory> mockWebSocketClientFactory = BuildMockWebSocketClientFactory(fakeServerIp, fakeServerPort, fakeToken);
        Mock<IPlexServerClient> mockPlexServerClient = BuildMockPlexServerClient(fakeServerIp, fakeServerPort, fakeToken);

        var strategy = new PlexSessionsWebSocketStrategy(
            new Mock<ILogger<PlexSessionsWebSocketStrategy>>().Object,
            mockPlexServerClient.Object,
            mockWebSocketClientFactory.Object
        );
        var result = new List<PlexSession>();
        const int elementsCountForTest = 3;
        // When
        await foreach (PlexSession plexSession in strategy.GetSessions("", fakeServerIp, fakeServerPort, fakeToken))
        {
            result.Add(plexSession);
            
            if (result.Count == elementsCountForTest)
            {
                strategy.Disconnect();
                break;
            }
        }

        // Then
        result
            .Should()
            .HaveCount(elementsCountForTest);

        var titles = result
            .Select(session => session.MediaTitle)
            .ToList();

        titles.Should().Contain("Test Media 0");
        titles.Should().Contain("Test Media 1");
        titles.Should().Contain("Test Media 2");
    }

    private static Mock<IPlexServerClient> BuildMockPlexServerClient(string fakeServerIp, int fakeServerPort,
        string fakeToken)
    {
        var mockPlexServerClient = new Mock<IPlexServerClient>();
        var plexServerHost = new Uri($"http://{fakeServerIp}:{fakeServerPort}").ToString();
        mockPlexServerClient.Setup(mock => mock.GetMediaMetadataAsync(
            fakeToken,
            plexServerHost,
            It.IsAny<string>()
        )).Returns<string, string, string>((_, _, mediaKey) => Task.FromResult(
            new MediaContainer
            {
                Media = new List<Metadata>
                {
                    new Metadata
                    {
                        Title = $"Test Media {mediaKey.Split("-").Last()}"
                    }
                }
            }
        ));
        return mockPlexServerClient;
    }

    private static Mock<IWebSocketClientFactory> BuildMockWebSocketClientFactory(string fakeServerIp,
        int fakeServerPort, string fakeToken)
    {
        IWebHostBuilder builder = WebHost.CreateDefaultBuilder()
            .UseStartup<FakeWebSocketsServer>()
            .UseEnvironment("Development");

        var server = new TestServer(builder);
        WebSocketClient wsClient = server.CreateWebSocketClient();

        Uri serverUrl = new UriBuilder(server.BaseAddress)
        {
            Scheme = "ws",
            Path = "ws"
        }.Uri;

        Func<Uri, CancellationToken, Task<WebSocket>> clientFactory = async Task<WebSocket>(url, cancellationToken) =>
            await wsClient.ConnectAsync(url, cancellationToken);

        var client = new WebsocketClient(
            serverUrl,
            clientFactory
        );

        var mockWebSocketClientFactory = new Mock<IWebSocketClientFactory>();
        mockWebSocketClientFactory
            .Setup(mock => mock.GetWebSocketClient(fakeServerIp, fakeServerPort, fakeToken))
            .Returns(() => client);
        return mockWebSocketClientFactory;
    }
}