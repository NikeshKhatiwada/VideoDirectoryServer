using System.ComponentModel.DataAnnotations;

namespace VideoDirectory_Server.Models
{
    public class Channel
    {
        [Required]
        [Key]
        public Guid Id { get; set; }
        [Required]
        public string Url { get; set; }
        [Required]
        public string Name { get; set; }
        [Required]
        public string Image { get; set; }
        [Required]
        public string Description { get; set; }
        public string? SiteLink { get; set; }
        public ICollection<Video> Videos { get; set; }
        public ICollection<ManagingUserChannel> ManagingUserChannels { get; set; }
        public ICollection<FollowingUserChannel> FollowingUserChannels { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }

        public Channel()
        {
            Videos = new List<Video>();
            ManagingUserChannels = new List<ManagingUserChannel>();
            FollowingUserChannels = new List<FollowingUserChannel>();
        }
    }
}
