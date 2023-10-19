using System.ComponentModel.DataAnnotations;

namespace VideoDirectory_Server.Dto.Video
{
    public class VideoInfoDto
    {
        [Required]
        public string Title { get; set; }
        [Required]
        public string Description { get; set; }
        [Required]
        public List<string> Tags { get; set; }
        [Required]
        public string Image { get; set; }
        [Required]
        public string ImageExtension { get; set; }
        [Required]
        public string ChannelUrl { get; set; }
    }
}
