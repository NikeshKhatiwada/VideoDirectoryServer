namespace VideoDirectory_Server.Services
{
    public class VideoUrlGenerator
    {
        public string GenerateUniqueShortUrl()
        {
            Guid guid = Guid.NewGuid();
            string base64Guid = Convert.ToBase64String(guid.ToByteArray());

            base64Guid = base64Guid.Replace("/", "_").Replace("+", "-");

            int desiredLength = 10;
            string shortUrl = base64Guid.Substring(0, desiredLength);

            while (IsShortUrlAlreadyExists(shortUrl))
            {
                guid = Guid.NewGuid();
                base64Guid = Convert.ToBase64String(guid.ToByteArray());
                base64Guid = base64Guid.Replace("/", "_").Replace("+", "-");
                shortUrl = base64Guid.Substring(0, desiredLength);
            }

            return shortUrl;
        }

        private static bool IsShortUrlAlreadyExists(string shortUrl)
        {
            return false;
        }
    }
}
