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
using NetCord.Services.Commands;

namespace DCMusicBot.Services
{
    // handle concurrent actions
    public partial class VoiceChannelActionService(IWebHostEnvironment env, ILogger<VoiceChannelActionService> logger)
    {
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

        public async Task EnqueuSongAsync(CommandContext context, ulong voiceChannelId, string url)
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

                SongInstruction song = new SongInstruction(context);

                await song.LoadSong(url);
                if (song.IsValidUrl)
                {
                    logger.LogInformation($"song id: [{song.YoutubeVideo.Id}], song title: [{song.YoutubeVideo.Title}]");
                    context.Message.ReplyAsync($"入咗Playlist {song.YoutubeVideo.Title}!");
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

        public async Task SkipAsync(ulong voiceChannelId)
        {
            try
            {
                if (!BotInChannel(voiceChannelId))
                {
                    throw new Exception("Bot not in channel");
                }

                VoiceConnection connection = currentConnections[voiceChannelId];

                await connection.SkipAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[StopAsync] exception");
            }
        }
        public async Task StopAsync(ulong voiceChannelId)
        {

            try
            {
                if (!BotInChannel(voiceChannelId))
                {
                    throw new Exception("Bot not in channel");
                }

                VoiceConnection connection = currentConnections[voiceChannelId];

                await connection.StopAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[StopAsync] exception");
            }
        }
    }
}
