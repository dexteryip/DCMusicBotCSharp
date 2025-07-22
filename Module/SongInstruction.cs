using YoutubeExplode.Videos;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using NetCord.Gateway;

namespace DCMusicBot.Module
{
    public class SongInstruction(Message requestMessage)
    {
        public bool IsValidUrl { get; private set; } = false;
        public Video? YoutubeVideo { get; private set; }
        public Message RequestMessage { get => requestMessage; }
        private string url;
        public async void LoadSong(string url)
        {
            this.url = url;
            IsValidUrl = true;

            var youtube = new YoutubeClient();

            YoutubeVideo = await youtube.Videos.GetAsync(url);

            if (YoutubeVideo != null)
            {
                IsValidUrl = true;
            }
        }
        public async Task<Stream?> GetVideoStream()
        {
            if (!IsValidUrl) return null;

            var youtube = new YoutubeClient();
            var streamManifest = await youtube.Videos.Streams.GetManifestAsync(url);
            var streamInfo = streamManifest.GetMuxedStreams().GetWithHighestVideoQuality();
            return await youtube.Videos.Streams.GetAsync(streamInfo);
        }
    }

}
