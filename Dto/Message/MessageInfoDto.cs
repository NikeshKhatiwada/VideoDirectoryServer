using System.ComponentModel.DataAnnotations;

namespace VideoDirectory_Server.Dto.Message
{
    public class MessageInfoDto
    {
        [Required]
        public string UserName { get; set; }
        [Required]
        public string Content { get; set; }
    }
}
