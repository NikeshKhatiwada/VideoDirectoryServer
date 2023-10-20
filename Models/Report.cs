using System.ComponentModel.DataAnnotations;

namespace VideoDirectory_Server.Models
{
    public class Report
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public User ReporterUser { get; set; }
        [Required]
        public string Reason { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }
    }

    public class VideoReport : Report
    {
        public int VideoId { get; set; }
        public Video Video { get; set; }
    }

    public class CommentReport : Report
    {
        public int CommentId { get; set; }
        public Comment Comment { get; set; }
    }

    public class UserReport : Report
    {
        public Guid ReportedUserId { get; set; }
        public User ReportedUser { get; set; }
    }

    public class ChannelReport : Report
    {
        public Guid ChannelId { get; set; }
        public Channel Channel { get; set; }
    }
}
