using System.ComponentModel.DataAnnotations;

namespace VideoDirectory_Server.Models
{
    public class AssociatedVideoTag
    {
        [Key]
        public int Id { get; set; }
        public int VideoId { get; set; }
        public Video Video { get; set; }
        public int TagId { get; set; }
        public Tag Tag { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }
    }
}
