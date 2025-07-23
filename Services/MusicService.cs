using DCMusicBot.Module;
using NetCord.Gateway;
using NetCord.Gateway.Voice;
using NetCord.Rest;
using NetCord.Services.Commands;
using System;

namespace DCMusicBot.Services
{
    public class MusicService(VoiceChannelActionService voiceChannelActionService)
    {
        public async Task<BotActionResult> JoinChat(CommandContext context)
        {
            // Get the user voice state
            if (!context.Guild!.VoiceStates.TryGetValue(context.User.Id, out var voiceState))
            {
                return new BotActionResult(false, "未入Chat join mud9");
            }
            ulong voiceChannelId = voiceState.ChannelId.GetValueOrDefault();

            if (voiceChannelActionService.BotInChannel(voiceChannelId)) return new BotActionResult();

            await voiceChannelActionService.JoinChatAsync(context.Guild!, context.Client, voiceChannelId);

            return new BotActionResult();
        }
        public async Task<BotActionResult> Play(CommandContext context, string[] args)
        {
            if (args.Length == 0 || !args[0].StartsWith("https://"))
            {
                return new BotActionResult(false, "?_? 咩Link");
            }
            string url = args[0];

            if (!(url.StartsWith("https://www.youtube.com") || url.StartsWith("https://youtube.com") || url.StartsWith("https://youtu.be")))
            {
                return new BotActionResult(false, "youtube link plz");
            }

            // Get the user voice state
            if (!context.Guild!.VoiceStates.TryGetValue(context.User.Id, out var voiceState))
            {
                return new BotActionResult(false, "未入Chat play mud9");
            }

            ulong voiceChannelId = voiceState.ChannelId.GetValueOrDefault();
            if (!voiceChannelActionService.BotInChannel(voiceChannelId))
            {
                await voiceChannelActionService.JoinChatAsync(context.Guild!, context.Client, voiceChannelId);
            }

            await voiceChannelActionService.EnqueuSongAsync(context, voiceChannelId, url);

            return new BotActionResult();
        }

        public async Task<BotActionResult> SkipAsync(CommandContext context)
        {
            // Get the user voice state
            if (!context.Guild!.VoiceStates.TryGetValue(context.User.Id, out var voiceState))
            {
                return new BotActionResult(false, "未入Chat skip mud9");
            }

            ulong voiceChannelId = voiceState.ChannelId.GetValueOrDefault();
            if (voiceChannelActionService.BotInChannel(voiceChannelId))
            {
                await voiceChannelActionService.SkipAsync(voiceChannelId);
            }

            return new BotActionResult();
        }
        public async Task<BotActionResult> StopAsync(CommandContext context)
        {
            // Get the user voice state
            if (!context.Guild!.VoiceStates.TryGetValue(context.User.Id, out var voiceState))
            {
                return new BotActionResult(false, "未入Chat stop mud9");
            }

            ulong voiceChannelId = voiceState.ChannelId.GetValueOrDefault();
            if (voiceChannelActionService.BotInChannel(voiceChannelId))
            {
                await voiceChannelActionService.StopAsync(voiceChannelId);
            }

            return new BotActionResult();
        }
    }
}
