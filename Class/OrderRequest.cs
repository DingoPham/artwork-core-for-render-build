using ArtworkCore.Models;

namespace ArtworkCore.Class
{
    public class OrderRequest
    {
        public List<SfwArt> Images { get; set; }
        public List<NsfwArt> NsfwImages { get; set; } = null;
        public List<Video> Videos { get; set; } = null;
        public List<NsfwVideo> NsfwVideos { get; set; } = null;
    }
}
