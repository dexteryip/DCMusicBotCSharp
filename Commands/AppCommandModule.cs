using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using NetCord;
using NetCord.Services.Commands;
using DCMusicBot.Services;
using DCMusicBot.Module;

namespace DCMusicBot.Commands
{
    public class AppCommandModule(BasicService basisService, MusicService musicService) : ApplicationCommandModule<ApplicationCommandContext>
    {
        [SlashCommand("hi", "hi")]
        public static string Ping() => "hi你老母";

        [SlashCommand("help", "?_?")]
        public string Help()
        {
            return basisService.Help();
        }

        //[UserCommand("ID")]
        //public static string Id(User user) => user.Id.ToString();

        //[MessageCommand("Timestamp")]
        //public static string Timestamp(RestMessage message) => message.CreatedAt.ToString();

        //[SlashCommand("play", "Play song")]
        //public async void Play(string url)
        //{
        //    var reqContext = new RequestContext(Context);
        //    var result = await musicService.Play(reqContext, url);

        //    if (!string.IsNullOrEmpty(result.Message))
        //    {
        //        await Context.Message.ReplyAsync(result.Message);
        //    }
        //}
    }
}
 