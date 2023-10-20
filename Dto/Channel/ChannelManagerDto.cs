using System.ComponentModel.DataAnnotations;

namespace VideoDirectory_Server.Dto.Channel
{
    public class ChannelManagerDto
    {
        [Required]
        public string UserName { get; set; }
        [Required]
        public string ChannelUrl { get; set;  }
    }
}
