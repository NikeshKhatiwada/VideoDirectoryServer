using System.ComponentModel.DataAnnotations;

namespace VideoDirectory_Server.Dto
{
    public class UserProfileImageDto
    {
        [Required]
        public byte[] ImageBytes { get; set; }
        [Required]
        public string ImageExtension { get; set; }
    }
}
