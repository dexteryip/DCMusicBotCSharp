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
using System.Diagnostics;

namespace DCMusicBot.Services
{
    // handle concurrent actions
    public class VoiceChannelActionService(IWebHostEnvironment env, ILogger<VoiceChannelActionService> logger)
    {
        private class VoiceConnection(ulong channel, ILogger logger)
        {
            public VoiceClient VoiceClient;
            public Stream OutStream;
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
                    logger.LogInformation($"[VoiceConnection] start playing song, channel {channel}");
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

                    logger.LogInformation($"[PlayerLoop][{channel}] start playing song [{currentSong.YoutubeVideo.Id}]{currentSong.YoutubeVideo.Title}");

                    TimeSpan? duration = currentSong.YoutubeVideo.Duration;
                    if (duration == null)
                    {
                        logger.LogInformation($"[PlayerLoop][{channel}] start playing song {currentSong.YoutubeVideo.Title}");
                        continue;
                    }

                    var task = currentSong.RequestMessage.ReplyAsync($"Playing {currentSong.YoutubeVideo.Title}!");
                    task.Wait();
                    var trackingMessage = task.Result;



                    var audioStreamInfo = await currentSong.GetAudioStreamInfo();
                    if (audioStreamInfo == null)
                    {
                        logger.LogInformation($"[PlayerLoop][{channel}] No audio stream found for this video");
                        break;
                    }


                    // Set up FFmpeg to process the audio stream
                    var startInfo = new ProcessStartInfo("ffmpeg")
                    {
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        Arguments = $"-i \"{audioStreamInfo.Url}\" -reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 5 -loglevel -8 -ac 2 -f s16le -ar 48000 pipe:1"
                    };

                    try
                    {
                        using var ffmpeg = Process.Start(startInfo);
                        if (ffmpeg == null)
                        {
                            logger.LogError($"[PlayerLoop][{channel}] Failed to start FFmpeg");
                            return;
                        }

                        // Create an Opus stream to encode audio
                        using var stream = new OpusEncodeStream(OutStream, PcmFormat.Short, VoiceChannels.Stereo, OpusApplication.Audio);

                        // Copy FFmpeg output to the Opus stream
                        await ffmpeg.StandardOutput.BaseStream.CopyToAsync(stream);
                        await stream.FlushAsync();
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, $"[PlayerLoop][{channel}] Error: {ex.Message}");
                    }

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
                VoiceConnection connection = currentConnections[voiceChannelId] = new VoiceConnection(voiceChannelId, logger);

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
                connection.OutStream = voiceClient.CreateOutputStream();
            }
            finally
            {
                connectionCreationSemaphore.Release();
            }
        }

        public async Task EnqueuSongAsync(Message requestMessage, ulong voiceChannelId, string url)
        {
            logger.LogInformation($"[EnqueuSongAsync] channel:{voiceChannelId} url: {url}");
            try
            {
                if (!BotInChannel(voiceChannelId))
                {
                    throw new Exception("Bot not in channel");
                }

                //if (env.IsDevelopment()) throw new Exception("no play music in dev!");

                VoiceConnection connection = currentConnections[voiceChannelId];

                SongInstruction song = new SongInstruction(requestMessage);

                await song.LoadSong(url);
                if (song.IsValidUrl)
                {
                    logger.LogInformation($"song id: [{song.YoutubeVideo.Id}], song title: [{song.YoutubeVideo.Title}]");
                    requestMessage.ReplyAsync($"入咗Playlist {song.YoutubeVideo.Title}!");
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
