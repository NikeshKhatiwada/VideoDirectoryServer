using FFMpegCore.Enums;
using FFMpegCore;
using System.Collections.Concurrent;
using VideoDirectory_Server.Data;
using VideoDirectory_Server.Models;
using Microsoft.EntityFrameworkCore;

namespace VideoDirectory_Server.Services
{
    public class VideoEncodingAndPublishingService
    {
        private readonly VideoTranscriptionService _transcriptionService;

        private readonly ConcurrentQueue<string> _videoUrls = new ConcurrentQueue<string>();

        List<(int Width, int Height, double FrameRate, int AudioBitRate)> targetQualities = new List<(int Width, int Height, double FrameRate, int AudioBitRate)>
        {
            (480, 480, 20, 128), //480p
            (360, 360, 15, 96), //360p
            (240, 240, 12, 64) //240p
        };

        string videosFolder = "Videos";
        string ffmpegPath = @"C:\Users\nikes\AppData\Local\Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-6.0-full_build\bin";

        private bool _isRunning = false;

        private readonly IServiceScopeFactory _serviceScopeFactory;

        public VideoEncodingAndPublishingService(IServiceScopeFactory serviceScopeFactory, VideoTranscriptionService transcriptionService)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _transcriptionService = transcriptionService;
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

            var videoInfo = FFProbe.Analyse(filePath);
            int originalWidth = videoInfo.PrimaryVideoStream.Width;
            int originalHeight = videoInfo.PrimaryVideoStream.Height;
            bool isPortrait = originalHeight > originalWidth;

            double originalFrameRate = videoInfo.PrimaryVideoStream.FrameRate;
            int originalAudioBitrate = (int)videoInfo.PrimaryAudioStream.BitRate / 1000;

            GlobalFFOptions.Configure(options => options.BinaryFolder = ffmpegPath);

            foreach (var (targetWidthValue, targetHeightValue, targetFrameRateValue, targetAudioBitrateValue) in targetQualities)
            {
                try
                {

                    int targetWidth = originalWidth;
                    int targetHeight = originalHeight;
                    if (!(originalWidth <= targetWidthValue && originalHeight <= targetHeightValue))
                    {
                        targetWidth = isPortrait ? targetWidthValue : (int)Math.Round((double)targetWidthValue * originalWidth / originalHeight);
                        targetWidth = targetWidth % 2 == 0 ? targetWidth : targetWidth - 1;

                        targetHeight = isPortrait ? (int)Math.Round((double)targetHeightValue * originalHeight / originalWidth) : targetHeightValue;
                        targetHeight = targetHeight % 2 == 0 ? targetHeight : targetHeight - 1;
                    }

                    double targetFrameRate = Math.Min(originalFrameRate, targetFrameRateValue);
                    int targetAudioBitrate = Math.Min(originalAudioBitrate, targetAudioBitrateValue);

                    string outputFileName = Path.GetFileNameWithoutExtension(filePath);
                    outputFileName = outputFileName.Substring(0, outputFileName.IndexOf("_"));
                    outputFileName = outputFileName + "_" + Math.Min(targetHeight, targetWidth) + "p.mp4";
                    string outputFilePath = Path.Combine(Path.GetDirectoryName(filePath), outputFileName);

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

                    var videoResolution = Math.Min(targetHeight, targetWidth) + "p";

                    string ipfsHash = await UploadToIPFS(outputFilePath);
                    video = dbContext.Videos.Include(v => v.VideoHashes).FirstOrDefault(v => v.Url == videoUrl);

                    if (video.VideoHashes != null && video.VideoHashes.Any())
                    {
                        var videoHashesList = video.VideoHashes.ToList();
                        foreach (var videoHash in videoHashesList)
                        {
                            if (videoHash.Resolution == videoResolution)
                            {
                                video.VideoHashes.Remove(videoHash);
                                dbContext.Videos.Update(video);
                            }
                        }
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
                    await dbContext.SaveChangesAsync();

                    video.IsPublished = true;
                    video.PublishedAt = DateTime.UtcNow;
                    video.LastUpdatedAt = DateTime.UtcNow;

                    dbContext.Videos.Update(video);
                    await dbContext.SaveChangesAsync();

                    File.Delete(outputFilePath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }

            _transcriptionService.AddVideoUrl(videoUrl);
            if (!(_transcriptionService.IsRunning))
            {
                _transcriptionService.Start();
            }
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
