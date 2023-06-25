using System.ComponentModel.DataAnnotations;

namespace VideoDirectory_Server.Models
{
    public class VideoLike
    {
        [Key, Required]
        public User User { get; set; }
        [Key, Required]
        public Video Video { get; set; }
        [Required]
        public bool LikeDislike { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdatedAt { get; set;}
    }
}
