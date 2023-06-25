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
        [Required]
        public string MainFilePath { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }
    }
}
