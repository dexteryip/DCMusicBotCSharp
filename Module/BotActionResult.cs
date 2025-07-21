namespace DCMusicBot.Module
{
    public class BotActionResult
    {
        public bool IsSuccess = true;
        public string Message = "";
        public BotActionResult(bool isSuccess = true, string result = "")
        {
            IsSuccess = isSuccess;
            Message = result;
        }
    }
}
