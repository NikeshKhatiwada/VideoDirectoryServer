using System.ComponentModel.DataAnnotations;

namespace VideoDirectory_Server.Dto
{
    public class AdminLoginDto
    {
        [Required]
        public string Username { get; set; }
        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }
}
