using System.ComponentModel.DataAnnotations;

namespace VideoDirectory_Server.Dto
{
    public class UserLoginDto
    {
        [Required]
        public string UserName { get; set; }
        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }
}
