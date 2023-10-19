using System.ComponentModel.DataAnnotations;

namespace VideoDirectory_Server.Dto.Channel
{
    public class ChannelDetailImageDto
    {
        [Required]
        public string Url { get; set; }
        [Required]
        public string Image { get; set; }
        [Required]
        public string ImageExtension { get; set; }
    }
}
