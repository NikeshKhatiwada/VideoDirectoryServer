using System.ComponentModel.DataAnnotations;

namespace VideoDirectory_Server.Models
{
    public class Transcript
    {
        [Key, Required]
        public int VideoId { get; set; }
        public Video Video { get; set; }
        [Required]
        public string Language { get; set; }
        [Required]
        public string Content { get; set; }
    }
}
