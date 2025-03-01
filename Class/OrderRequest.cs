using ArtworkCore.Models;

namespace ArtworkCore.Class
{
    public class OrderRequest
    {
        public List<SfwArt> Images { get; set; }
        public List<NsfwArt> NsfwImages { get; set; }
        public List<Video> Videos { get; set; }
        public List<NsfwVideo> NsfwVideos { get; set; }
    }
}
