using System.ComponentModel.DataAnnotations;

namespace VideoDirectory_Server.Models
{
    public class User
    {
        [Required]
        [Key]
        public Guid Id { get; set; }
        [Required]
        public string FirstName { get; set; }
        [Required]
        public string LastName { get; set; }
        [Required]
        public string UserName { get; set; }
        [Required]
        public string Email { get; set; }
        [Required]
        public string Image { get; set; }
        [Required]
        public string Password { get; set; }
        public DateTime? SuspendedUntil { get; set; }
        public ICollection<ManagingUserChannel> ManagingUserChannels { get; set; }
        public ICollection<FollowingUserChannel> FollowingUserChannels { get; set; }
        public ICollection<VideoView> VideoViews { get; set; }
        public ICollection<VideoLike> VideoLikes { get; set; }
        public ICollection<Message> SentMessages { get; set; }
        public ICollection<Message> ReceivedMessages { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }
        
        public User()
        {
            ManagingUserChannels = new List<ManagingUserChannel>();
            FollowingUserChannels = new List<FollowingUserChannel>();
            VideoViews = new List<VideoView>();
            VideoLikes = new List<VideoLike>();
            SentMessages = new List<Message>();
            ReceivedMessages = new List<Message>();
        }
    }
}
