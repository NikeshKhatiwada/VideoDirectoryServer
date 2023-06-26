using System.ComponentModel.DataAnnotations;

namespace VideoDirectory_Server.Models
{
    public class User
    {
        [Required]
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
        public DateTime CreatedAt { get; set; }
        public DateTime LastModifiedAt { get; set;}
        public DateTime SuspendedUntil { get; set; }
        public List<Channel> ManagedChannels { get; set; }
        public List<Channel> FollowedChannels { get; set; }
    }
}
