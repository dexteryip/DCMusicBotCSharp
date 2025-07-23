using YoutubeExplode.Videos;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using NetCord.Gateway;
using System.Linq;

namespace DCMusicBot.Module
{
    public class SongInstruction(Message requestMessage)
    {
        public bool IsValidUrl { get; private set; } = false;
        public Video? YoutubeVideo { get; private set; }
        public Message RequestMessage { get => requestMessage; }
        private string url;
        public async Task LoadSong(string url)
        {
            this.url = url;

            var youtube = new YoutubeClient();

            YoutubeVideo = await youtube.Videos.GetAsync(url);

            if (YoutubeVideo != null)
            {
                IsValidUrl = true;
            }
        }
        public async Task<IStreamInfo> GetAudioStreamInfo()
        {
            if (!IsValidUrl) return null;

            var youtube = new YoutubeClient();
            var streamManifest = await youtube.Videos.Streams.GetManifestAsync(url);
            var streams = streamManifest.GetAudioOnlyStreams();
            var stream = streams.Where(s => s.Bitrate > new Bitrate(50 * 1000)).OrderBy(s => s.Bitrate).First(); // min above 50 kbps
            if (stream == null)
                stream = streams.OrderBy(s => s.Bitrate).First();
            //return streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
            return stream;
        }
    }

}
