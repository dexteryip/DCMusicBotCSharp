using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using NetCord;
using NetCord.Services.Commands;
using DCMusicBot.Services;

namespace DCMusicBot.Commands
{
    public class AppCommandModule(BasicService basisService) : ApplicationCommandModule<ApplicationCommandContext>
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
    }
}
 