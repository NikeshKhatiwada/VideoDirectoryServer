using FFMpegCore.Enums;
using FFMpegCore;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using VideoDirectory_Server.Data;
using VideoDirectory_Server.Dto.Video;
using VideoDirectory_Server.Models;
using FFMpegCore.Arguments;

namespace VideoDirectory_Server.Services
{
    public class VideoEditingService
    {
        private readonly ConcurrentQueue<VideoEditingDto> _videos = new ConcurrentQueue<VideoEditingDto>();

        static string videosFolder = "Videos";
        static string ffmpegPath = @"C:\Users\nikes\AppData\Local\Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-6.0-full_build\bin";

        private bool _isRunning = false;

        private readonly IServiceScopeFactory _serviceScopeFactory;

        public VideoEditingService(IServiceScopeFactory serviceScopeFactory)
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
                    if (_videos.TryDequeue(out VideoEditingDto videoWithEditing))
                    {
                        using (var scope = _serviceScopeFactory.CreateScope())
                        {
                            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                            await EditVideo(videoWithEditing.VideoUrl,
                                videoWithEditing.TrimFrom, videoWithEditing.TrimTo,
                                videoWithEditing.RotationOrFlipValue,
                                videoWithEditing.AspectRatioNumerator, videoWithEditing.AspectRatioDenominator,
                                videoWithEditing.TextLogo,
                                dbContext);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
        }

        public async Task EditVideo(string videoUrl, 
            TimeSpan trimFrom, TimeSpan trimTo, 
            int rotationOrFlipValue, 
            int aspectRatioNumerator, int aspectRatioDenominator, 
            string textLogo, 
            ApplicationDbContext dbContext)
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

            TimeSpan initialTime = TimeSpan.Zero;
            TimeSpan completeTime = videoInfo.Duration;

            int targetWidth = originalWidth;
            int targetHeight = originalHeight;

            double targetFrameRate = Math.Min(originalFrameRate, 24);
            int targetAudioBitrate = Math.Min(originalAudioBitrate, (int)AudioQuality.Good);

            string outputFileName = Path.GetFileNameWithoutExtension(filePath) + "_" + "edited.mp4";
            string outputFilePath = Path.Combine(Path.GetDirectoryName(filePath), outputFileName);

            GlobalFFOptions.Configure(options => options.BinaryFolder = ffmpegPath);

            if (!(trimFrom == initialTime && trimTo == completeTime))
            {
                FFMpegArguments
                .FromFileInput(filePath)
                .OutputToFile(outputFilePath, true, options => options
                    .WithVideoCodec(VideoCodec.LibX264)
                    .WithAudioCodec(AudioCodec.Aac)
                    .Seek(trimFrom).EndSeek(trimTo)
                    .WithFastStart())
                .ProcessSynchronously();

                File.Delete(filePath);
                File.Move(outputFilePath, filePath);
            }

            if (rotationOrFlipValue == 0 || rotationOrFlipValue == 1 || rotationOrFlipValue == 2 || rotationOrFlipValue == 3)
            {
                FFMpegArguments
                .FromFileInput(filePath)
                .OutputToFile(outputFilePath, true, options => options
                    .WithVideoCodec(VideoCodec.LibX264)
                    .WithAudioCodec(AudioCodec.Aac)
                    .WithVideoFilters(filterOptions => filterOptions.Transpose((Transposition)rotationOrFlipValue))
                    .WithFastStart())
                .ProcessSynchronously();

                File.Delete(filePath);
                File.Move(outputFilePath, filePath);
            }

            if (!(aspectRatioNumerator == 0 || aspectRatioDenominator == 0))
            {
                videoInfo = FFProbe.Analyse(filePath);
                originalWidth = videoInfo.PrimaryVideoStream.Width;
                originalHeight = videoInfo.PrimaryVideoStream.Height;
                isPortrait = originalHeight > originalWidth;

                if (isPortrait)
                {
                    targetHeight = originalHeight;
                    targetWidth = (int)Math.Round((double)targetHeight * aspectRatioNumerator / aspectRatioDenominator);
                }
                else
                {
                    targetWidth = originalWidth;
                    targetHeight = (int)Math.Round((double)targetWidth * aspectRatioDenominator / aspectRatioNumerator);
                }
                targetWidth = targetWidth % 2 == 0 ? targetWidth : targetWidth - 1;
                targetHeight = targetHeight % 2 == 0 ? targetHeight : targetHeight - 1;

                FFMpegArguments
                .FromFileInput(filePath)
                .OutputToFile(outputFilePath, true, options => options
                    .WithVideoCodec(VideoCodec.LibX264)
                    .WithAudioCodec(AudioCodec.Aac)
                    .WithVideoFilters(filterOptions => filterOptions.Scale(targetWidth, targetHeight))
                    .WithFastStart())
                .ProcessSynchronously();

                File.Delete(filePath);
                File.Move(outputFilePath, filePath);
            }

            if (!(textLogo == null || textLogo.Length == 0))
            {
                FFMpegArguments
                .FromFileInput(filePath)
                .OutputToFile(outputFilePath, true, options => options
                    .WithVideoCodec(VideoCodec.LibX264)
                    .WithAudioCodec(AudioCodec.Aac)
                    .WithVideoFilters(filterOptions => filterOptions.DrawText(DrawTextOptions
                        .Create(textLogo, "Fonts/Lobster-Regular.ttf")
                            .WithParameter("fontcolor", "white")
                            .WithParameter("fontsize", "32")
                            .WithParameter("box", "1")
                            .WithParameter("boxcolor", "black@0.5")
                            .WithParameter("boxborderw", "5")
                            .WithParameter("x", "w-text_w-10")
                            .WithParameter("y", "10")))
                    .WithFastStart())
                .ProcessSynchronously();

                File.Delete(filePath);
                File.Move(outputFilePath, filePath);
            }

            video.LastUpdatedAt = DateTime.UtcNow;
            dbContext.Videos.Update(video);
            await dbContext.SaveChangesAsync();

            var videoResolution = Math.Min(targetHeight, targetWidth) + "p";

            string ipfsHash = await UploadToIPFS(outputFilePath);
            video = dbContext.Videos.Include(v => v.VideoHashes).FirstOrDefault(v => v.Url == videoUrl);
            foreach (var videoHash in video.VideoHashes)
            {
                video.VideoHashes.Remove(videoHash);
                dbContext.Videos.Update(video);
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

        public void AddVideo(VideoEditingDto videoWithEditing)
        {
            _videos.Enqueue(videoWithEditing);
        }

        public bool IsRunning => _isRunning;
    }
}
