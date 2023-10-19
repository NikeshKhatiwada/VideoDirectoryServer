using FFMpegCore;
using FFMpegCore.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Linq;
using VideoDirectory_Server.Data;
using VideoDirectory_Server.Models;

namespace VideoDirectory_Server.Services
{
    public class InitialVideoEncodingService
    {
        private readonly ConcurrentQueue<string> _videoUrls = new ConcurrentQueue<string>();

        string videosFolder = "Videos";
        string ffmpegPath = @"C:\Users\nikes\AppData\Local\Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-6.0-full_build\bin";

        private bool _isRunning = false;

        private readonly IServiceScopeFactory _serviceScopeFactory;

        public InitialVideoEncodingService(IServiceScopeFactory serviceScopeFactory)
        {
            _serviceScopeFactory = serviceScopeFactory;
        }

        public async Task Start()
        {
            _isRunning = true;
            while (true)
            {
                try
                {
                    if (_videoUrls.TryDequeue(out string videoUrl))
                    {
                        using (var scope = _serviceScopeFactory.CreateScope())
                        {
                            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                            await EncodeVideo(videoUrl, dbContext);
                        }
                    }
                    else
                    {
                        await Task.Delay(10000);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
        }

        private async Task EncodeVideo(string videoUrl, ApplicationDbContext dbContext)
        {
            var video = dbContext.Videos.FirstOrDefault(v => v.Url == videoUrl);
            var videoName = video.MainFilePath;

            string filePath = Path.Combine(videosFolder, videoName);

            if (!File.Exists(filePath))
            {
                return;
            }

            // Get video resolution using FFMPEGCore
            var videoInfo = FFProbe.Analyse(filePath);
            int originalWidth = videoInfo.PrimaryVideoStream.Width;
            int originalHeight = videoInfo.PrimaryVideoStream.Height;
            bool isPortrait = originalHeight > originalWidth;

            double originalFrameRate = videoInfo.PrimaryVideoStream.FrameRate;
            int originalAudioBitrate = (int)videoInfo.PrimaryAudioStream.BitRate / 1000;

            int targetWidth = originalWidth;
            int targetHeight = originalHeight;

            if (!(originalWidth <= 720 && originalHeight <= 720))
            {
                targetWidth = isPortrait ? 720 : (int)Math.Round(720.0 * originalWidth / originalHeight);
                targetWidth = targetWidth % 2 == 0 ? targetWidth : targetWidth - 1;

                targetHeight = isPortrait ? (int)Math.Round(720.0 * originalHeight / originalWidth) : 720;
                targetHeight = targetHeight % 2 == 0 ? targetHeight : targetHeight - 1;
            }

            double targetFrameRate = Math.Min(originalFrameRate, 24);
            int targetAudioBitrate = Math.Min(originalAudioBitrate, (int)AudioQuality.Good);

            // Create the output file path with the same location and name as the original file
            string outputFileName = Path.GetFileNameWithoutExtension(filePath) + "_" + Math.Min(targetHeight, targetWidth) + "p.mp4";
            string outputFilePath = Path.Combine(Path.GetDirectoryName(filePath), outputFileName);

            // Set up the FFmpeg global options
            GlobalFFOptions.Configure(options => options.BinaryFolder = ffmpegPath);

            FFMpegArguments
                .FromFileInput(filePath)
                .OutputToFile(outputFilePath, true, options => options
                    .WithVideoCodec(VideoCodec.LibX264)
                    .WithAudioCodec(AudioCodec.Aac)
                    .WithAudioBitrate(targetAudioBitrate)
                    .WithFramerate(targetFrameRate)
                    .WithVideoFilters(filterOptions => filterOptions.Scale(targetWidth, targetHeight))
                    .WithFastStart())
                .ProcessSynchronously();

            video.MainFilePath = outputFileName;
            video.LastUpdatedAt = DateTime.UtcNow;
            dbContext.Videos.Update(video);
            await dbContext.SaveChangesAsync();

            var videoResolution = Math.Min(targetHeight, targetWidth) + "p";

            string ipfsHash = await UploadToIPFS(outputFilePath);
            video = dbContext.Videos.Include(v => v.VideoHashes).FirstOrDefault(v => v.Url == videoUrl);
            foreach(var videoHash in video.VideoHashes)
            {
                video.VideoHashes.Remove(videoHash);
            }
            var newVideoHash = new VideoHash
            {
                Video = video,
                Resolution = videoResolution,
                Hash = ipfsHash,
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow
            };
            video.VideoHashes.Add(newVideoHash);
            dbContext.Videos.Update(video);
            dbContext.SaveChanges();

            File.Delete(filePath);
        }

        private async Task<string> UploadToIPFS(string filePath)
        {
            using (var client = new HttpClient())
            {
                string apiEndpoint = "http://localhost:5001/api/v0/";

                var multipartContent = new MultipartFormDataContent();
                using (var fileStream = File.OpenRead(filePath))
                {
                    var fileName = Path.GetFileName(filePath);
                    multipartContent.Add(new StreamContent(fileStream), "file", fileName);
                    var response = await client.PostAsync(apiEndpoint + "add", multipartContent);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseContent = await response.Content.ReadAsStringAsync();
                        string ipfsHash = ParseHashFromResponse(responseContent);
                        return ipfsHash;
                    }
                    else
                    {
                        throw new Exception($"Failed to upload to IPFS. StatusCode: {response.StatusCode}");
                    }
                }
            }
        }

        static string ParseHashFromResponse(string responseContent)
        {
            int startIndex = responseContent.IndexOf("\"Hash\":\"") + "\"Hash\":\"".Length;
            int endIndex = responseContent.IndexOf("\"", startIndex);

            string ipfsHash = responseContent.Substring(startIndex, endIndex - startIndex);
            return ipfsHash;
        }

        public void AddVideoUrl(string videoUrl)
        {
            _videoUrls.Enqueue(videoUrl);
        }

        public bool IsRunning => _isRunning;
    }
}
