using NetCord.Gateway.Voice;
using NetCord.Gateway;
using NetCord.Logging;
using DCMusicBot.Module;
using System.Collections.Concurrent;
using System;
using NetCord.Services.Commands;
using System.Threading;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace DCMusicBot.Services
{
    // handle concurrent actions
    public class ChatroomActionService
    {
        private class BotConnection(ulong channel)
        {
            public SemaphoreSlim Semaphore = new SemaphoreSlim(1, 1);
            public VoiceClient VoiceClient;
            public CancellationTokenSource cancellationTokenSource = new();
            public ValueTask OnVoiceChannelDisconnect(DisconnectEventArgs args)
            {
                if (!args.Reconnect)
                {
                    cancellationTokenSource.Cancel();
                    currentConnections.Remove(channel, out var con);
                }
                return default;
            }

        }
        private static ConcurrentDictionary<ulong, BotConnection> currentConnections = new();

        public async Task JoinChatAsync(CommandContext context, ulong voiceChannelId)
        {
            if (currentConnections.ContainsKey(voiceChannelId))
            {
                return;
            }
            BotConnection connection;
            connection = currentConnections[voiceChannelId] = new BotConnection(voiceChannelId);

            var guild = context.Guild!;
            var client = context.Client;

            var semaphore = connection.Semaphore;
            await semaphore.WaitAsync();
            try
            {
                VoiceClient voiceClient = await client.JoinVoiceChannelAsync(
                        guild.Id,
                        voiceChannelId,
                        new VoiceClientConfiguration
                        {
                            Logger = new ConsoleLogger(),
                        });
                voiceClient.Disconnect += connection.OnVoiceChannelDisconnect;

                // Connect
                await voiceClient.StartAsync();
                await voiceClient.EnterSpeakingStateAsync(new(SpeakingFlags.Microphone));

                connection.VoiceClient = voiceClient;
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task PlayAsync(CommandContext context, ulong voiceChannelId, string url)
        {
            await JoinChatAsync(context, voiceChannelId);
        }

        public async Task GetYoutubeStream()
        {
            var youtube = new YoutubeClient();

            var videoUrl = "https://youtube.com/watch?v=u_yIGGhubZs";


            var video = await youtube.Videos.GetAsync(videoUrl);

            var title = video.Title; // "Collections - Blender 2.80 Fundamentals"
            var author = video.Author.ChannelTitle; // "Blender"
            var duration = video.Duration; // 00:07:20



            var streamManifest = await youtube.Videos.Streams.GetManifestAsync(videoUrl);
            var streamInfo = streamManifest.GetMuxedStreams().GetWithHighestVideoQuality();
            var stream = await youtube.Videos.Streams.GetAsync(streamInfo);
            await youtube.Videos.Streams.DownloadAsync(streamInfo, $"video.{streamInfo.Container}");

        }
    }
}
