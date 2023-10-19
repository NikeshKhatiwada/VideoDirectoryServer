namespace VideoDirectory_Server.Dto.Video
{
    public class VideoFilteringDto
    {
        public string VideoUrl { get; set; }
        public char GrayscaleOrSepiaType { get; set; }
        public char ColorTintType { get; set; }
        public int BrightnessValue { get; set; }
        public int ContrastValue { get; set; }
        public int SaturationValue { get; set; }
    }
}
