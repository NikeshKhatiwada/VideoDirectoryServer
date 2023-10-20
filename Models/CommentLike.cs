using System.ComponentModel.DataAnnotations;

namespace VideoDirectory_Server.Models
{
    public class CommentLike
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public User User { get; set; }
        [Required]
        public Comment Comment { get; set; }
        [Required]
        public bool LikeDislike { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }
    }
}
