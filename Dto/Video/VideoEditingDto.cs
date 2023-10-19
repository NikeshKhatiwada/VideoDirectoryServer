namespace VideoDirectory_Server.Dto.Video
{
    public class VideoEditingDto
    {
        public string VideoUrl { get; set; }
        public TimeSpan TrimFrom { get; set; }
        public TimeSpan TrimTo { get; set; }
        public int RotationOrFlipValue { get; set; }
        public int AspectRatioNumerator { get; set; }
        public int AspectRatioDenominator { get; set; }
        public string TextLogo { get; set; }
    }
}
