using System.ComponentModel.DataAnnotations;

namespace VideoDirectory_Server.Dto.Report
{
    public class ReportInfoDto
    {
        [Required]
        public string Reason { get; set; }
        [Required]
        public char ContentType { get; set; }
        [Required]
        public string ContentIdentifier { get; set; }
    }
}
