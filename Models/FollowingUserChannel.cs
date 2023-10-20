using System.ComponentModel.DataAnnotations;

namespace VideoDirectory_Server.Models
{
    public class FollowingUserChannel
    {
        [Key]
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public User User { get; set; }

        public Guid ChannelId { get; set; }
        public Channel Channel { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }
    }
}
