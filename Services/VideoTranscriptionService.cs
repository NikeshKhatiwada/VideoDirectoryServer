using FFMpegCore;
using Microsoft.EntityFrameworkCore;
//using Python.Runtime;
using System.Collections.Concurrent;
using System.Text.Json;
using VideoDirectory_Server.Data;
using VideoDirectory_Server.Models;

namespace VideoDirectory_Server.Services
{
    public class VideoTranscriptionService
    {
        private readonly IConfiguration _configuration;

        private readonly ConcurrentQueue<string> _videoUrls = new ConcurrentQueue<string>();

        string videosFolder = "Videos";
        string audiosFolder = @"Services\Python\Audios";
        string ffmpegPath = @"C:\Users\nikes\AppData\Local\Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-6.0-full_build\bin";

        string falconServerUrl = "http://localhost:7558/transcribe";

        private bool _isRunning = false;

        private readonly IServiceScopeFactory _serviceScopeFactory;

        public VideoTranscriptionService(IServiceScopeFactory serviceScopeFactory, IConfiguration configuration)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _configuration = configuration;
        }

        public async Task Start()
        {
            _isRunning = true;
            while (true)
            {
                if (_videoUrls.TryDequeue(out string videoUrl))
                {
                    using (var scope = _serviceScopeFactory.CreateScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        await TranscribeVideo(videoUrl, dbContext);
                    }
                }
                else
                {
                    await Task.Delay(10000);
                }
            }
        }

        private async Task TranscribeVideo(string videoUrl, ApplicationDbContext dbContext)
        {
            GlobalFFOptions.Configure(options => options.BinaryFolder = ffmpegPath);
            var projectPath = _configuration.GetValue<string>("Directory:ProjectDirectory");

            try
            {
                var video = dbContext.Videos.Include(v => v.Transcript).FirstOrDefault(v => v.Url == videoUrl);
                var videoName = video.MainFilePath;
                var videoTranscript = dbContext.Transcripts.FirstOrDefault(vt => vt.VideoId == video.Id);

                string filePath = Path.Combine(videosFolder, videoName);

                if (!File.Exists(filePath))
                {
                    return;
                }

                string outputAudioName = Path.GetFileNameWithoutExtension(filePath) + ".mp3";
                var outputAudioPath = Path.Combine(audiosFolder, outputAudioName);

                FFMpeg.ExtractAudio(filePath, outputAudioPath);
                //(string language, string transcript) = GetTranscript(outputAudioName, projectPath);

                (string language, string transcript) = await GetTranscriptAsync(falconServerUrl, outputAudioName);
                if (videoTranscript != null)
                {
                    dbContext.Transcripts.Remove(videoTranscript);
                    dbContext.SaveChanges();
                }

                var newTranscript = new Transcript
                {
                    VideoId = video.Id,
                    Language = language,
                    Content = transcript
                };

                video.Transcript = newTranscript;
                video.LastUpdatedAt = DateTime.UtcNow;

                dbContext.Update(video);
                await dbContext.SaveChangesAsync();

                File.Delete(outputAudioPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        static async Task<(string language, string transcript)> GetTranscriptAsync(string serverUrl, string audioFileName)
        {
            using (HttpClient client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromMinutes(40);
                var request = new
                {
                    audio_file_name = audioFileName
                };

                HttpResponseMessage response = await client.PostAsJsonAsync(serverUrl, request);

                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();

                    var jsonDoc = JsonDocument.Parse(jsonResponse);
                    var root = jsonDoc.RootElement;

                    string language = root.GetProperty("language").GetString();
                    string transcript = root.GetProperty("transcript").GetString();

                    return (language, transcript);
                }
                else
                {
                    throw new Exception("Request failed with status code: " + response.StatusCode);
                }
            }
        }

        //static (string, string) GetTranscript(string audioName, string projectPath)
        //{
        //    string pythonDll = @"C:\Users\nikes\AppData\Local\Programs\Python\Python311\python311.dll";
        //    Environment.SetEnvironmentVariable("PYTHONNET_PYDLL", pythonDll);

        //    string audioFileName = audioName;

        //    PythonEngine.Initialize();

        //    using (Py.GIL())
        //    {
        //        dynamic sys = Py.Import("sys");
        //        sys.path.append(Path.Combine(projectPath, "Services"));
        //        dynamic module = Py.Import("transcription");
        //        dynamic transcribeAudioFunction = module.GetAttr("transcribe_audio");
        //        PyTuple result = transcribeAudioFunction(audioFileName);
        //        string language = result[0].As<string>();
        //        string transcript = result[1].As<string>();

        //        return (language, transcript);
        //    }
        //}

        public void AddVideoUrl(string videoUrl)
        {
            _videoUrls.Enqueue(videoUrl);
        }

        public bool IsRunning => _isRunning;
    }
}
