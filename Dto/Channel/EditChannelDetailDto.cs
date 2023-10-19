using System.ComponentModel.DataAnnotations;

namespace VideoDirectory_Server.Dto.Channel
{
    public class EditChannelDetailDto
    {
        [Required]
        public string Name { get; set; }
        [Required]
        public string Description { get; set; }
        public string? SiteLink { get; set; }
    }
}
