using DCMusicBot.Module;
using DCMusicBot.Services;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.Commands;
using System;

namespace DCMusicBot.Commands
{
    public class TextCommandModule(BasicService basicService, MusicService musicService) : CommandModule<CommandContext>
    {
        [Command("help")]
        public string Help()
        {
            return basicService.Help();
        }

        [Command("hi")]
        public string Ping()
        {
            return basicService.Hi();
        }

        // debug
        [Command("join")]
        public async void Join()
        {
            var result = await musicService.JoinChat(Context);

            if (!string.IsNullOrEmpty(result.Message))
            {
                await Context.Message.ReplyAsync(result.Message);
            }
        }

        [Command("p", "play")]
        public async void Play(params string[] url)
        {
            var result = await musicService.Play(Context, url);

            if (!string.IsNullOrEmpty(result.Message))
            {
                await Context.Message.ReplyAsync(result.Message);
            }
        }
    }
}
