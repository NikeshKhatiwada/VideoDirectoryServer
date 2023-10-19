using System.ComponentModel.DataAnnotations;

namespace VideoDirectory_Server.Models
{
    public class Tag
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string Name { get; set; }
        public ICollection<AssociatedVideoTag> AssociatedVideoTags { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }

        public Tag()
        {
            AssociatedVideoTags = new List<AssociatedVideoTag>();
        }
    }
}
