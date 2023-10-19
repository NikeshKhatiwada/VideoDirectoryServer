using System.ComponentModel.DataAnnotations;

namespace VideoDirectory_Server.Dto
{
    public class UserRegistrationDto
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

        [Required]
        public byte[] SelectedImage { get; set; }
        [Required]
        public string SelectedImageExtension { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }
}
