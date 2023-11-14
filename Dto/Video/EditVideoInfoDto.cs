using System.ComponentModel.DataAnnotations;

namespace VideoDirectory_Server.Dto.Video
{
    public class EditVideoInfoDto
    {
        [Required]
        public string Title { get; set; }
        [Required]
        public string Description { get; set; }
        [Required]
        public List<string> Tags { get; set; }
    }
}
