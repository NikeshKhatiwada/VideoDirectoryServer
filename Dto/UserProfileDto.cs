using System.ComponentModel.DataAnnotations;

namespace VideoDirectory_Server.Dto
{
    public class UserProfileDto
    {
        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        [Required]
        public string UserName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}
