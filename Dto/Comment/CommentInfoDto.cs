using System.ComponentModel.DataAnnotations;

namespace VideoDirectory_Server.Dto.Comment
{
    public class CommentInfoDto
    {
        [Required]
        public string Description { get; set; }
        [Required]
        public string VideoUrl { get; set; }
    }
}
