using System.ComponentModel.DataAnnotations;
using System.Reflection.Metadata;

namespace VideoDirectory_Server.Models
{
    public class VideoHash
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public Video Video { get; set; }
        [Required]
        public string Resolution { get; set; }
        [Required]
        public string Hash { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }
    }
}
