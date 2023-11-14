using FFMpegCore;
using FFMpegCore.Enums;
using Microsoft.EntityFrameworkCore;
using OpenCvSharp;
using SixLabors.ImageSharp.Processing.Processors.Filters;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using VideoDirectory_Server.Data;
using VideoDirectory_Server.Dto.Video;
using VideoDirectory_Server.Models;
using Size = OpenCvSharp.Size;

namespace VideoDirectory_Server.Services
{
    public class VideoFilteringService
    {
        private readonly ConcurrentQueue<VideoFilteringDto> _videos = new ConcurrentQueue<VideoFilteringDto>();

        static string videosFolder = "Videos";
        static string ffmpegPath = @"C:\Users\nikes\AppData\Local\Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-6.0-full_build\bin";

        private bool _isRunning = false;

        private readonly IServiceScopeFactory _serviceScopeFactory;

        public VideoFilteringService(IServiceScopeFactory serviceScopeFactory)
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
                    if (_videos.TryDequeue(out VideoFilteringDto videoWithFiltering))
                    {
                        using (var scope = _serviceScopeFactory.CreateScope())
                        {
                            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                            await ApplyVideoFilters(videoWithFiltering.VideoUrl,
                                videoWithFiltering.GrayscaleOrSepiaType, videoWithFiltering.ColorTintType,
                                videoWithFiltering.BrightnessValue,
                                videoWithFiltering.ContrastValue,
                                videoWithFiltering.SaturationValue,
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

        public async Task ApplyVideoFilters(string videoUrl, 
            char artisticFilterType, char colorFilterType, 
            int brightnessValue, int contrastValue, int saturationValue, 
            ApplicationDbContext dbContext)
        {
            var video = dbContext.Videos.FirstOrDefault(v => v.Url == videoUrl);
            var videoName = video.MainFilePath;

            string filePath = Path.Combine(videosFolder, videoName);

            if (!File.Exists(filePath))
            {
                return;
            }

            string outputFileName = Path.GetFileNameWithoutExtension(filePath);
            outputFileName = outputFileName.Substring(0, outputFileName.IndexOf("_filtered.mp4"));
            string outputFilePath = Path.Combine(Path.GetDirectoryName(filePath), outputFileName);

            using var capture = new VideoCapture(filePath);
            using var writer = new VideoWriter(outputFilePath, FourCC.X264, capture.Fps, new Size(capture.FrameWidth, capture.FrameHeight));

            Action<Image<Rgba32>>[] filters = {
                image => ApplyGrayscaleOrSepiaFilter(image, artisticFilterType),
                image => ApplyChannelsTintFilter(image, colorFilterType),
                image => ApplyBrightnessCorrection(image, brightnessValue),
                image => ApplyContrastCorrection(image, contrastValue),
                //ApplyHueModification,
                image => ApplySaturationCorrection(image, saturationValue)
            };

            var frame = new Mat();
            while (capture.Read(frame))
            {
                using (var image = MatToImage(frame))
                {
                    ApplyFilters(filters, image);
                    writer.Write(ImageToMat(image));
                }
            }

            string outputAudioName = Path.GetFileNameWithoutExtension(videoName) + ".mp3";
            var outputAudioPath = Path.Combine(videosFolder, outputAudioName);

            await SaveVideo(outputFilePath, filePath, outputAudioPath);

            await SaveVideoToIPFS(filePath, videoUrl, dbContext);

            video.LastUpdatedAt = DateTime.UtcNow;
            dbContext.Videos.Update(video);
            await dbContext.SaveChangesAsync();
        }

        public async Task SaveVideoToIPFS(string videoPath, string videoUrl, ApplicationDbContext dbContext)
        {
            GlobalFFOptions.Configure(options => options.BinaryFolder = ffmpegPath);

            var videoInfo = FFProbe.Analyse(videoPath);

            int originalWidth = videoInfo.PrimaryVideoStream.Width;
            int originalHeight = videoInfo.PrimaryVideoStream.Height;
            var videoResolution = Math.Min(originalHeight, originalWidth) + "p";

            string ipfsHash = await UploadToIPFS(videoPath);
            var video = dbContext.Videos.Include(v => v.VideoHashes).FirstOrDefault(v => v.Url == videoUrl);
            foreach (var videoHash in video.VideoHashes.ToList())
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
            dbContext.SaveChanges();
        }

        static async Task SaveVideo(string inputVideoPath, string outputVideoPath, string outputAudioPath)
        {
            GlobalFFOptions.Configure(options => options.BinaryFolder = ffmpegPath);

            var videoInfo = FFProbe.Analyse(outputVideoPath);

            FFMpeg.ExtractAudio(outputVideoPath, outputAudioPath);

            int originalWidth = videoInfo.PrimaryVideoStream.Width;
            int originalHeight = videoInfo.PrimaryVideoStream.Height;

            double originalFrameRate = videoInfo.PrimaryVideoStream.FrameRate;
            int originalAudioBitrate = (int)videoInfo.PrimaryAudioStream.BitRate / 1000;
            int originalVideoBitrate = (int)videoInfo.PrimaryVideoStream.BitRate / 1000;

            await AddAudio(inputVideoPath, outputAudioPath);

            FFMpegArguments
                .FromFileInput(inputVideoPath)
                .OutputToFile(outputVideoPath, true, options => options
                    .WithVideoCodec(VideoCodec.LibX264)
                    .WithAudioCodec(AudioCodec.Aac)
                    .WithVideoBitrate(originalVideoBitrate)
                    .WithAudioBitrate(originalAudioBitrate)
                    .WithFramerate(originalFrameRate)
                    .WithVideoFilters(filterOptions => filterOptions.Scale(originalWidth, originalHeight))
                    .WithFastStart())
                .ProcessSynchronously();
        }

        static async Task AddAudio(string inputVideoPath, string inputAudioPath)
        {
            GlobalFFOptions.Configure(options => options.BinaryFolder = ffmpegPath);

            string outputFileName = Path.GetFileNameWithoutExtension(Path.GetRandomFileName()) + ".mp4";
            var outputVideoPath = Path.Combine(videosFolder, outputFileName);

            FFMpeg.ReplaceAudio(inputVideoPath, inputAudioPath, outputVideoPath);

            File.Delete(inputVideoPath);
            File.Move(outputVideoPath, inputVideoPath);

            File.Delete(inputAudioPath);
        }

        static void ApplyFilters(Action<Image<Rgba32>>[] filters, Image<Rgba32> image)
        {
            foreach (var filter in filters)
            {
                filter(image);
            }
        }

        static void ApplyBrightnessCorrection(Image<Rgba32> image, int brightnessValue)
        {
            if (brightnessValue != 100)
            {
                float brightness = (float)(Convert.ToDouble(brightnessValue) / 100);
                image.Mutate(ctx => ctx.ApplyProcessor(new BrightnessProcessor(brightness)));
            }
        }

        static void ApplyContrastCorrection(Image<Rgba32> image, int contrastValue)
        {
            if (contrastValue != 100)
            {
                float contrast = (float)(Convert.ToDouble(contrastValue) / 100);
                image.Mutate(ctx => ctx.ApplyProcessor(new ContrastProcessor(contrast)));
            }
        }

        static void ApplyHueModification(Image<Rgba32> image)
        {
            image.Mutate(ctx => ctx.ApplyProcessor(new HueProcessor(90)));
        }

        static void ApplySaturationCorrection(Image<Rgba32> image, int saturationValue)
        {
            if (saturationValue != 100)
            {
                float saturation = (float)(Convert.ToDouble(saturationValue) / 100);
                image.Mutate(ctx => ctx.ApplyProcessor(new SaturateProcessor(saturation)));
            }
        }

        static void ApplyChannelsTintFilter(Image<Rgba32> image, char tintType)
        {
            var reddishColor = new Rgba32(255, 0, 0, 150);
            var bluishColor = new Rgba32(0, 0, 255, 150);
            var greenishColor = new Rgba32(0, 255, 0, 150);

            Rgba32 tint = new Rgba32(0, 0, 0, 0);
            if (tintType == 'R')
                tint = reddishColor;
            else if (tintType == 'B')
                tint = bluishColor;
            else if (tintType == 'G')
                tint = greenishColor;

            if (tintType != 'N')
            {
                using (var overlay = new Image<Rgba32>(image.Width, image.Height))
                {
                    for (int y = 0; y < image.Height; y++)
                    {
                        for (int x = 0; x < image.Width; x++)
                        {
                            Rgba32 pixel = image[x, y];
                            float alphaBlend = tint.A / 255f;
                            image[x, y] = new Rgba32(
                                (byte)(pixel.R * (1 - alphaBlend) + tint.R * alphaBlend),
                                (byte)(pixel.G * (1 - alphaBlend) + tint.G * alphaBlend),
                                (byte)(pixel.B * (1 - alphaBlend) + tint.B * alphaBlend),
                                pixel.A);
                        }
                    }
                }
            }
        }

        static void ApplyGrayscaleOrSepiaFilter(Image<Rgba32> image, char filterType)
        {
            if (filterType != 'N')
            {
                if (filterType == 'G')
                    image.Mutate(ctx => ctx.Grayscale());
                if (filterType == 'S')
                    image.Mutate(ctx => ctx.Sepia());
            }
        }

        static Image<Rgba32> MatToImage(Mat mat)
        {
            var image = new Image<Rgba32>(mat.Width, mat.Height);

            for (int y = 0; y < mat.Height; y++)
            {
                for (int x = 0; x < mat.Width; x++)
                {
                    Vec3b pixel = mat.At<Vec3b>(y, x);
                    image[x, y] = new Rgba32(pixel.Item2, pixel.Item1, pixel.Item0); // OpenCv uses BGR order
                }
            }

            return image;
        }

        static Mat ImageToMat(Image<Rgba32> image)
        {
            var mat = new Mat(image.Height, image.Width, MatType.CV_8UC3);

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    Rgba32 pixel = image[x, y];
                    mat.Set(y, x, new Vec3b(pixel.B, pixel.G, pixel.R)); // OpenCv uses BGR order
                }
            }

            return mat;
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

        public void AddVideo(VideoFilteringDto videoWithFiltering)
        {
            _videos.Enqueue(videoWithFiltering);
        }

        public bool IsRunning => _isRunning;
    }
}
