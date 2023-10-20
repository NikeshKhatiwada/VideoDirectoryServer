using System.ComponentModel.DataAnnotations;

namespace VideoDirectory_Server.Models
{
    public class VideoView
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public User User { get; set; }
        [Required]
        public Video Video { get; set; }
        [Required]
        public int ViewCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }
    }
}
