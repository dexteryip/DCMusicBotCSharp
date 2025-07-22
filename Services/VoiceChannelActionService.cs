using NetCord.Gateway.Voice;
using NetCord.Gateway;
using NetCord.Logging;
using System.Collections.Concurrent;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode.Videos;
using DCMusicBot.Module;
using NetCord.Rest;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace DCMusicBot.Services
{
    // handle concurrent actions
    public class VoiceChannelActionService(IWebHostEnvironment env, ILogger<VoiceChannelActionService> logger)
    {
        private class VoiceConnection(ulong channel)
        {
            public VoiceClient VoiceClient;
            public Stream audioStream;
            private CancellationTokenSource skipTokenSource = new();
            private SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
            public bool isPlaying = false;
            Task player;
            public ConcurrentQueue<SongInstruction> songs = new ConcurrentQueue<SongInstruction>();
            public ValueTask OnVoiceChannelDisconnect(DisconnectEventArgs args)
            {
                if (!args.Reconnect)
                {
                    Task.Run(Stop);
                    currentConnections.Remove(channel, out var con);
                }
                return default;
            }

            public async Task StartPlaying()
            {
                try
                {
                    semaphore.Wait();
                    if (isPlaying) return;
                    if (songs.Count == 0) return;
                    isPlaying = true;
                    player = Task.Run(PlayerLoop);
                }
                finally
                {
                    semaphore.Release();
                }
            }
            public void Skip()
            {
                try
                {
                    semaphore.Wait();
                    skipTokenSource.Cancel();
                }
                finally
                {
                    semaphore.Release();
                }
            }
            public void Stop()
            {
                try
                {
                    semaphore.Wait();
                    songs.Clear();
                    skipTokenSource.Cancel();
                }
                finally
                {
                    semaphore.Release();
                }
            }
            private async void PlayerLoop()
            {
                while (songs.Count > 0)
                {
                    if (!songs.TryDequeue(out SongInstruction currentSong)) continue;

                    TimeSpan? duration = currentSong.YoutubeVideo.Duration;
                    if (duration != null) continue;

                    var task = currentSong.RequestMessage.ReplyAsync($"Playing {currentSong.YoutubeVideo.Title}!");
                    task.Wait();
                    var trackingMessage = task.Result;


                    // We create this stream to automatically convert the PCM data returned by FFmpeg to Opus data.
                    // The Opus data is then written to 'outStream' that sends the data to Discord
                    OpusEncodeStream stream = new(audioStream, PcmFormat.Short, VoiceChannels.Stereo, OpusApplication.Audio);

                    var getStreamTask = currentSong.GetVideoStream();
                    var videoStream = getStreamTask.Result;
                    videoStream.CopyTo(stream);

                    TimeSpan waitTotal = duration ?? TimeSpan.Zero;
                    if (waitTotal != TimeSpan.Zero) waitTotal += new TimeSpan(0, 0, 5);
                    bool isSkipped = skipTokenSource.Token.WaitHandle.WaitOne(waitTotal);
                    if (isSkipped)
                    {
                        // stop song
                    }

                    trackingMessage.DeleteAsync();
                }
                isPlaying = false;
            }
        }

        public bool BotInChannel(ulong voiceChannelId)
        {
            return currentConnections.ContainsKey(voiceChannelId);
        }

        private static ConcurrentDictionary<ulong, VoiceConnection> currentConnections = new();
        private static SemaphoreSlim connectionCreationSemaphore = new SemaphoreSlim(1, 1);

        public async Task JoinChatAsync(Guild guild, GatewayClient client, ulong voiceChannelId)
        {
            await connectionCreationSemaphore.WaitAsync();
            try
            {
                if (BotInChannel(voiceChannelId))
                {
                    return;
                }
                VoiceConnection connection = currentConnections[voiceChannelId] = new VoiceConnection(voiceChannelId);

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
                connection.audioStream = voiceClient.CreateOutputStream();
            }
            finally
            {
                connectionCreationSemaphore.Release();
            }
        }

        public async Task EnqueuSongAsync(Message requestMessage, ulong voiceChannelId, string url)
        {
            logger.LogInformation($"[EnqueuSongAsync] channel:{voiceChannelId} url: {url}");
            if (!BotInChannel(voiceChannelId))
            {
                throw new Exception("Bot not in channel");
            }
            if (env.IsDevelopment()) throw new Exception("no play music in dev!");

            VoiceConnection connection = currentConnections[voiceChannelId];

            SongInstruction song = new SongInstruction(requestMessage);
            try
            {
                song.LoadSong(url);
                if (song.IsValidUrl)
                {
                    logger.LogInformation($"song id: [{song.YoutubeVideo.Id}], song title: [{song.YoutubeVideo.Title}]");
                    connection.songs.Enqueue(song);
                    connection.StartPlaying();
                }
                else
                {

                    logger.LogInformation("[EnqueuSongAsync] invalid url: " + url);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[EnqueuSongAsync] exception: " + url);
            }
        }
    }
}
