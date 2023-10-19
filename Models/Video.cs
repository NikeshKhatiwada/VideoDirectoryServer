using System.ComponentModel.DataAnnotations;

namespace VideoDirectory_Server.Models
{
    public class Video
    {
        [Key, Required]
        public int Id { get; set; }
        [Required]
        public Channel Channel { get; set; }
        [Required]
        public string Title { get; set; }
        [Required]
        public string Url { get; set; }
        [Required]
        public string Description { get; set; }
        public ICollection<AssociatedVideoTag> AssociatedVideoTags { get; set; }
        [Required]
        public string Thumbnail { get; set; }
        [Required]
        public string MainFilePath { get; set; }
        public ICollection<VideoHash> VideoHashes { get; set; }
        public ICollection<VideoView> VideoViews { get; set; }
        public ICollection<VideoLike> VideoLikes { get; set; }
        public ICollection<Comment> Comments { get; set; }
        public Transcript? Transcript { get; set; }
        [Required]
        public bool IsPublished { get; set; } = false;
        public DateTime? PublishedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }

        public Video()
        {
            AssociatedVideoTags = new List<AssociatedVideoTag>();
            VideoHashes = new List<VideoHash>();
            VideoViews = new List<VideoView>();
            VideoLikes = new List<VideoLike>();
            Comments = new List<Comment>();
        }
    }
}
