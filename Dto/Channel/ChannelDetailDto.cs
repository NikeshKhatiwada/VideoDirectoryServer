using System.ComponentModel.DataAnnotations;

namespace VideoDirectory_Server.Dto.Channel
{
    public class ChannelDetailDto
    {
        [Required]
        public string Name { get; set; }

        [Required]
        public string Url { get; set; }

        [Required]
        public string Image { get; set; }
        [Required]
        public string ImageExtension { get; set; }
        [Required]
        public string Description { get; set; }
        public string? SiteLink { get; set; }
    }
}
