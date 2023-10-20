using System.ComponentModel.DataAnnotations;

namespace VideoDirectory_Server.Models
{
    public class ManagingUserChannel
    {
        [Key]
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public User User { get; set; }

        public Guid ChannelId { get; set; }
        public Channel Channel { get; set; }

        public char Privilege { get; set; } = 'O';

        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }
    }
}
