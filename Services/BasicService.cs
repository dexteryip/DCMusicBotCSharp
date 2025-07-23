namespace DCMusicBot.Services
{
    public class BasicService(ILogger<BasicService> logger)
    {
        public string Help()
        {
            return "help你老母";
        }
        public string Hi()
        {
            logger.LogInformation("Test hi");
            return "hi你老母";
        }
    }
}
