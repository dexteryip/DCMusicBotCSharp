using DCMusicBot.Module;
using NetCord.Rest;
using NetCord.Services.Commands;
using System;

namespace DCMusicBot.Services
{
    public class MusicService(ChatroomActionService chatroomActionService)
    {
        public async Task<BotActionResult> JoinChat(CommandContext context)
        {
            // Get the user voice state
            if (!context.Guild!.VoiceStates.TryGetValue(context.User.Id, out var voiceState))
            {
                return new BotActionResult(false, "未入Chat join mud9");
            }
            await chatroomActionService.JoinChatAsync(context, voiceState.ChannelId.GetValueOrDefault());

            return new BotActionResult();
        }
        public async Task<BotActionResult> Play(CommandContext context, string[] args)
        {
            if (args.Length == 0 || !args[0].StartsWith("https://"))
            {
                return new BotActionResult(false, "?_? 咩Link");
            }
            string url = args[0];
            // Get the user voice state
            if (!context.Guild!.VoiceStates.TryGetValue(context.User.Id, out var voiceState))
            {
                return new BotActionResult(false, "未入Chat play mud9");
            }

            await chatroomActionService.PlayAsync(context, voiceState.ChannelId.GetValueOrDefault(), url);

            return new BotActionResult();
        }
    }
}
