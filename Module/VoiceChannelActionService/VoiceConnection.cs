using NetCord.Gateway.Voice;
using NetCord.Gateway;
using System.Collections.Concurrent;
using System.Diagnostics;
using DCMusicBot.Module;
using NetCord.Logging;
using System;

namespace DCMusicBot.Services
{
    public partial class VoiceChannelActionService
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
                    Task.Run(StopAsync);
                    if (currentConnections.ContainsKey(channel))
                    {
                        currentConnections.Remove(channel, out var con);
                    }
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
            public async Task SkipAsync()
            {
                try
                {
                    await semaphore.WaitAsync();
                    skipTokenSource.Cancel();
                }
                finally
                {
                    semaphore.Release();
                }
            }
            public async Task StopAsync()
            {
                try
                {
                    await semaphore.WaitAsync();
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
                        logger.LogError($"[PlayerLoop][{channel}] cannot get song duration {currentSong.YoutubeVideo.Id}");
                        continue;
                    }

                    var playingText = await currentSong.RequestContext.Channel!.SendMessageAsync($"Playing {currentSong.YoutubeVideo.Title}!");
                    //var task = currentSong.RequestContext.Message.ReplyAsync($"Playing {currentSong.YoutubeVideo.Title}!");
                    //task.Wait();
                    //var playingText = task.Result;








                    var audioStreamInfo = await currentSong.GetAudioStreamInfo();
                    if (audioStreamInfo == null)
                    {
                        logger.LogError($"[PlayerLoop][{channel}] No audio stream found for this video");
                        break;
                    }


                    // Set up FFmpeg to process the audio stream
                    var startInfo = new ProcessStartInfo("ffmpeg")
                    {
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        Arguments = $"-i \"{audioStreamInfo.Url}\" -reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 5 -tune zerolatency -loglevel -8 -ac 2 -f s16le -ar 48000 pipe:1"
                    };

                    Process? ffmpeg = null;
                    try
                    {
                        ffmpeg = Process.Start(startInfo);
                        if (ffmpeg == null)
                        {
                            logger.LogError($"[PlayerLoop][{channel}] Failed to start FFmpeg");
                            return;
                        }

                        // Create an Opus stream to encode audio
                        using var stream = new OpusEncodeStream(OutStream, PcmFormat.Short, VoiceChannels.Stereo, OpusApplication.Audio);

                        // create new token for skipping
                        skipTokenSource = new();

                        // Copy FFmpeg output to the Opus stream
                        try
                        {
                            await ffmpeg.StandardOutput.BaseStream.CopyToAsync(stream, skipTokenSource.Token);
                            await stream.FlushAsync(skipTokenSource.Token);
                        }
                        catch (TaskCanceledException) { } //expected task cancel

                        // if song is not skipped, wait 3 seconds for next song
                        if (skipTokenSource.IsCancellationRequested)
                        {
                            logger.LogInformation($"[PlayerLoop][{channel}] song skipped");
                        }
                        else
                        {
                            logger.LogInformation($"[PlayerLoop][{channel}] continue to next song in 3 seconds");
                            await Task.Delay(3000);
                        }
                        // stop song
                        ffmpeg.Kill();

                        await currentSong.RequestContext.Channel.DeleteMessageAsync(playingText.Id);

                        await currentSong.RequestContext.Client.UpdateVoiceStateAsync(new VoiceStateProperties(currentSong.RequestContext.Guild.Id, channel));
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, $"[PlayerLoop][{channel}] Error: {ex.Message}");
                    }
                    finally
                    {
                        ffmpeg?.Dispose();
                    }
                }
                isPlaying = false;

            }
        }
    }
}
