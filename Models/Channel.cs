using System.ComponentModel.DataAnnotations;

namespace VideoDirectory_Server.Models
{
    public class Channel
    {
        [Required]
        public Guid Id { get; set; }
        [Required]
        public string Name { get; set; }
        [Required]
        public string Image { get; set; }
        [Required]
        public string Description { get; set; }
        [Required]
        public string Url { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }
        public List<User> ManagingUsers { get; set; }
    }
}
