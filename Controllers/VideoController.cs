using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using VideoDirectory_Server.Data;
using VideoDirectory_Server.Dto.Channel;
using VideoDirectory_Server.Dto.Video;
using VideoDirectory_Server.Models;
using VideoDirectory_Server.Services;

namespace VideoDirectory_Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class VideoController : ControllerBase
    {
        private ApplicationDbContext? Context { get; }
        private readonly IConfiguration _configuration;

        private readonly InitialVideoEncodingService _initialEncodingService;
        private readonly VideoEncodingAndPublishingService _encodingAndPublishingService;
        private readonly VideoEditingService _videoEditingService;
        private readonly VideoFilteringService _videoFilteringService;

        private VideoUrlGenerator VideoUrlGenerator { get; }

        public VideoController(ApplicationDbContext context, IConfiguration configuration,
            VideoUrlGenerator videoUrlGenerator,
            InitialVideoEncodingService encodingService,
            VideoEncodingAndPublishingService encodingAndPublishingService,
            VideoEditingService videoEditingService,
            VideoFilteringService videoFilteringService)
        {
            this.Context = context;
            _configuration = configuration;
            VideoUrlGenerator = videoUrlGenerator;
            _initialEncodingService = encodingService;
            _encodingAndPublishingService = encodingAndPublishingService;
            _videoEditingService = videoEditingService;
            _videoFilteringService = videoFilteringService;
        }

        [HttpPost]
        [Route("/video/upload")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(500 * 1024 * 1024)]
        public IActionResult UploadVideo(
            /*[FromForm] MultipartFormDataContent multipartFormData*/
            [FromForm] IFormFile video,
            [FromForm] string videoInfo)
        {
            try
            {
                string secretKey = _configuration.GetSection("Key:SecretKey").Value;
                var key = Encoding.UTF8.GetBytes(secretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromHours(24)
                };

                var authorizationHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
                {
                    return Unauthorized();
                }

                var token = authorizationHeader.Substring("Bearer ".Length);

                var tokenHandler = new JwtSecurityTokenHandler();

                SecurityToken validatedToken;
                var principal = tokenHandler.ValidateToken(token, validationParameters, out validatedToken);

                var usernameClaim = principal.FindFirst("Username");
                if (usernameClaim == null)
                {
                    return Unauthorized();
                }

                var username = usernameClaim.Value;

                var user = this.Context.Users.FirstOrDefault(u => u.UserName == username);
                if (user == null)
                {
                    return Unauthorized();
                }

                user = this.Context.Users.Include(u => u.ManagingUserChannels).FirstOrDefault(u => u.UserName == username);

                //MultipartFormDataContent multipartFormData = new MultipartFormDataContent();

                //if (multipartFormData == null)
                //{
                //    return BadRequest("Invalid request.");
                //}

                //var videoInfo = multipartFormData.FirstOrDefault(content => content.Headers.ContentDisposition.Name == "\"videoInfo\"");
                if (videoInfo == null)
                {
                    return BadRequest("Video metadata not found.");
                }

                //var videoInfoJson = videoInfo.ReadAsStringAsync().Result;
                VideoInfoDto videoInfoDto = JsonSerializer.Deserialize<VideoInfoDto>(videoInfo/*videoInfoJson*/);

                var channel = this.Context.Channels.FirstOrDefault(c => c.Url == videoInfoDto.ChannelUrl);
                if (channel == null)
                {
                    return NotFound("Channel doesn't exist.");
                }

                var managedChannels = user.ManagingUserChannels;
                var isManagerOfChannel = managedChannels.Any(mc => mc.ChannelId == channel.Id);

                if (!isManagerOfChannel)
                {
                    return Unauthorized();
                }

                //var videoFile = multipartFormData.FirstOrDefault(file => file.Headers.ContentDisposition.Name == "\"video\"");
                var videoFile = video;
                if (videoFile == null)
                {
                    return BadRequest("Video file not found.");
                }

                var videoStream = videoFile.OpenReadStream();
                var videoFileName = videoFile.FileName;

                var newVideo = new Video
                {
                    Channel = channel,
                    Title = videoInfoDto.Title,
                    Url = VideoUrlGenerator.GenerateUniqueShortUrl(),
                    Description = videoInfoDto.Description,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow
                };

                if (videoStream != null)
                {
                    string uploadPath = Path.Combine("", "Videos");
                    string fileName = Path.GetRandomFileName();
                    string filePath = Path.Combine(uploadPath, fileName);
                    using (Stream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                    {
                        videoStream.CopyTo(fileStream);
                    }
                    FileInfo fileInfo = new FileInfo(filePath);
                    string videoFileExtension = Path.GetExtension(videoFileName);
                    fileName = fileName.Substring(0, fileName.IndexOf("."));
                    fileName = fileName + videoFileExtension;
                    if (fileInfo.Exists)
                    {
                        fileInfo.MoveTo(Path.Combine(uploadPath, fileName));
                    }
                    videoStream.Dispose();
                    newVideo.MainFilePath = fileName;
                }

                if (videoInfoDto.Image != null)
                {
                    byte[] imageData = Convert.FromBase64String(videoInfoDto.Image);
                    string uploadPath = Path.Combine("", "Thumbnails");
                    string fileName = Path.GetRandomFileName();
                    string filePath = Path.Combine(uploadPath, fileName);
                    using (Stream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                    {
                        fileStream.Write(imageData, 0, imageData.Length);
                    }
                    FileInfo fileInfo = new FileInfo(filePath);
                    var fileExtension = videoInfoDto.ImageExtension;
                    fileName = fileName.Substring(0, fileName.IndexOf("."));
                    fileName = fileName + fileExtension;
                    if (fileInfo.Exists)
                    {
                        fileInfo.MoveTo(Path.Combine(uploadPath, fileName));
                    }
                    newVideo.Thumbnail = fileName;
                }

                Context.Videos.Add(newVideo);
                Context.SaveChanges();

                var uploadedVideo = this.Context.Videos.Include(v => v.AssociatedVideoTags).FirstOrDefault(v => v.Url == newVideo.Url);

                foreach (string tagName in videoInfoDto.Tags)
                {
                    var isAlreadyInDatabase = this.Context.Tags.Any(t => t.Name == tagName);
                    if (!(isAlreadyInDatabase))
                    {
                        var newTag = new Tag
                        {
                            Name = tagName,
                            CreatedAt = DateTime.UtcNow,
                            LastUpdatedAt = DateTime.UtcNow
                        };
                        Context.Tags.Add(newTag);
                        Context.SaveChanges();
                    }

                    var tag = this.Context.Tags.FirstOrDefault(t => t.Name == tagName);
                    var associatedVideoTag = new AssociatedVideoTag
                    {
                        VideoId = uploadedVideo.Id,
                        TagId = tag.Id,
                        CreatedAt = tag.CreatedAt,
                        LastUpdatedAt = tag.LastUpdatedAt
                    };
                    uploadedVideo.AssociatedVideoTags.Add(associatedVideoTag);
                }

                Context.Videos.Update(uploadedVideo);
                Context.SaveChanges();

                _initialEncodingService.AddVideoUrl(newVideo.Url);
                if (!(_initialEncodingService.IsRunning))
                {
                    _initialEncodingService.Start();
                }

                return Ok("Video upload successful.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [Route("/videos/following")]
        public IActionResult GetFollowingChannelVideos()
        {
            try
            {
                string secretKey = _configuration.GetSection("Key:SecretKey").Value;
                var key = Encoding.UTF8.GetBytes(secretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromHours(24)
                };

                var authorizationHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
                {
                    return Unauthorized();
                }

                var token = authorizationHeader.Substring("Bearer ".Length);

                var tokenHandler = new JwtSecurityTokenHandler();

                SecurityToken validatedToken;
                var principal = tokenHandler.ValidateToken(token, validationParameters, out validatedToken);

                var usernameClaim = principal.FindFirst("Username");
                if (usernameClaim == null)
                {
                    return Unauthorized();
                }

                var username = usernameClaim.Value;

                var user = this.Context.Users.FirstOrDefault(u => u.UserName == username);
                if (user == null)
                {
                    return Unauthorized();
                }

                user = this.Context.Users.Include(u => u.FollowingUserChannels)
                    .ThenInclude(ufc => ufc.Channel)
                    .ThenInclude(c => c.Videos)
                    .FirstOrDefault(u => u.UserName == username);

                List<Video> followedChannelsVideos = user.FollowingUserChannels
                    .SelectMany(ufc => ufc.Channel.Videos)
                    .ToList();

                if (!(followedChannelsVideos.Any()))
                {
                    return NotFound("No videos found.");
                }

                List<object> FollowedVideoItems = new List<object>();
                foreach (Video video in followedChannelsVideos)
                {
                    string thumbnailUploadPath = Path.Combine("", "Thumbnails");
                    string thumbnailFileName = video.Thumbnail;
                    string thumbnailFilePath = Path.Combine(thumbnailUploadPath, thumbnailFileName);
                    FileInfo thumbnailFileInfo = new FileInfo(thumbnailFilePath);
                    if (!thumbnailFileInfo.Exists)
                    {
                        return NotFound("Thumbnail not found.");
                    }

                    byte[] thubnailBytes;

                    using (FileStream fileStream = thumbnailFileInfo.OpenRead())
                    {
                        using (MemoryStream memoryStream = new MemoryStream())
                        {
                            fileStream.CopyTo(memoryStream);
                            thubnailBytes = memoryStream.ToArray();
                        }
                    }

                    string base64Thumbnail = Convert.ToBase64String(thubnailBytes);

                    string uploadPath = Path.Combine("", "Channels");
                    string fileName = video.Channel.Image;
                    string filePath = Path.Combine(uploadPath, fileName);
                    FileInfo fileInfo = new FileInfo(filePath);
                    if (!fileInfo.Exists)
                    {
                        return NotFound("Image not found.");
                    }

                    byte[] imageBytes;

                    using (FileStream fileStream = fileInfo.OpenRead())
                    {
                        using (MemoryStream memoryStream = new MemoryStream())
                        {
                            fileStream.CopyTo(memoryStream);
                            imageBytes = memoryStream.ToArray();
                        }
                    }

                    string base64Image = Convert.ToBase64String(imageBytes);

                    var VideoItemModel = new
                    {
                        Thumbnail = base64Thumbnail,
                        VideoUrl = video.Url,
                        Title = video.Title,
                        ChannelName = video.Channel.Name,
                        ChannelImage = base64Image,
                        ChannelUrl = video.Channel.Url,
                        PublishedAt = video.PublishedAt
                    };

                    FollowedVideoItems.Add(VideoItemModel);
                }

                if (FollowedVideoItems.Count > 0)
                {
                    return Ok(FollowedVideoItems);
                }

                return NotFound("No videos found.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [Route("/videos/search")]
        public IActionResult GetSearchedVideos(string value)
        {
            try
            {
                string secretKey = _configuration.GetSection("Key:SecretKey").Value;
                var key = Encoding.UTF8.GetBytes(secretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromHours(24)
                };

                var authorizationHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
                {
                    return Unauthorized();
                }

                var token = authorizationHeader.Substring("Bearer ".Length);

                var tokenHandler = new JwtSecurityTokenHandler();

                SecurityToken validatedToken;
                var principal = tokenHandler.ValidateToken(token, validationParameters, out validatedToken);

                var usernameClaim = principal.FindFirst("Username");
                if (usernameClaim == null)
                {
                    return Unauthorized();
                }

                var username = usernameClaim.Value;

                var user = this.Context.Users.FirstOrDefault(u => u.UserName == username);
                if (user == null)
                {
                    return Unauthorized();
                }

                if (string.IsNullOrEmpty(value))
                {
                    return BadRequest("Search query is empty.");
                }

                var keywords = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                var searchedVideos = this.Context.Videos
                    .Include(v => v.AssociatedVideoTags)
                        .ThenInclude(avt => avt.Tag)
                        .Include(v => v.Channel)
                        .ToList()
                    .Where(v =>
                        keywords.Any(keyword => v.Title.Contains(keyword)) ||
                        keywords.Any(keyword => v.Description.Contains(keyword)) ||
                        v.AssociatedVideoTags.Any(avt =>
                            keywords.Any(keyword => avt.Tag.Name.Contains(keyword))
                        )
                    )
                    .ToList();

                if (!(searchedVideos.Any()))
                {
                    return NotFound("No videos found.");
                }

                List<object> SearchedVideoItems = new List<object>();
                foreach (Video video in searchedVideos)
                {
                    string thumbnailUploadPath = Path.Combine("", "Thumbnails");
                    string thumbnailFileName = video.Thumbnail;
                    string thumbnailFilePath = Path.Combine(thumbnailUploadPath, thumbnailFileName);
                    FileInfo thumbnailFileInfo = new FileInfo(thumbnailFilePath);
                    if (!thumbnailFileInfo.Exists)
                    {
                        return NotFound("Thumbnail not found.");
                    }

                    byte[] thubnailBytes;

                    using (FileStream fileStream = thumbnailFileInfo.OpenRead())
                    {
                        using (MemoryStream memoryStream = new MemoryStream())
                        {
                            fileStream.CopyTo(memoryStream);
                            thubnailBytes = memoryStream.ToArray();
                        }
                    }

                    string base64Thumbnail = Convert.ToBase64String(thubnailBytes);

                    string uploadPath = Path.Combine("", "Channels");
                    string fileName = video.Channel.Image;
                    string filePath = Path.Combine(uploadPath, fileName);
                    FileInfo fileInfo = new FileInfo(filePath);
                    if (!fileInfo.Exists)
                    {
                        return NotFound("Image not found.");
                    }

                    byte[] imageBytes;

                    using (FileStream fileStream = fileInfo.OpenRead())
                    {
                        using (MemoryStream memoryStream = new MemoryStream())
                        {
                            fileStream.CopyTo(memoryStream);
                            imageBytes = memoryStream.ToArray();
                        }
                    }

                    string base64Image = Convert.ToBase64String(imageBytes);

                    var VideoItemModel = new
                    {
                        Thumbnail = base64Thumbnail,
                        VideoUrl = video.Url,
                        Title = video.Title,
                        ChannelName = video.Channel.Name,
                        ChannelImage = base64Image,
                        ChannelUrl = video.Channel.Url,
                        PublishedAt = video.PublishedAt
                    };

                    SearchedVideoItems.Add(VideoItemModel);
                }

                if (SearchedVideoItems.Count > 0)
                {
                    return Ok(SearchedVideoItems);
                }

                return NotFound("No videos found.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [Route("/video/{videoUrl}/managed/details")]
        public IActionResult GetManagedVideoDetails(string videoUrl)
        {
            try
            {
                string secretKey = _configuration.GetSection("Key:SecretKey").Value;
                var key = Encoding.UTF8.GetBytes(secretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromHours(24)
                };

                var authorizationHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
                {
                    return Unauthorized();
                }

                var token = authorizationHeader.Substring("Bearer ".Length);

                var tokenHandler = new JwtSecurityTokenHandler();

                SecurityToken validatedToken;
                var principal = tokenHandler.ValidateToken(token, validationParameters, out validatedToken);

                var usernameClaim = principal.FindFirst("Username");
                if (usernameClaim == null)
                {
                    return Unauthorized();
                }

                var username = usernameClaim.Value;

                var user = this.Context.Users.FirstOrDefault(u => u.UserName == username);
                if (user == null)
                {
                    return Unauthorized();
                }

                user = this.Context.Users.Include(u => u.ManagingUserChannels).FirstOrDefault(u => u.UserName == username);

                var video = this.Context.Videos.Include(v => v.Channel).FirstOrDefault(v => v.Url == videoUrl);
                if (video == null)
                {
                    return NotFound("Video doesn't exist.");
                }

                var managedChannels = user.ManagingUserChannels;
                var isManagerOfChannel = managedChannels.Any(mc => mc.ChannelId == video.Channel.Id);

                if (!isManagerOfChannel)
                {
                    return Unauthorized();
                }

                video = this.Context.Videos
                    .Include(v => v.Channel)
                    .Include(v => v.AssociatedVideoTags)
                    .ThenInclude(avt => avt.Tag)
                    .FirstOrDefault(v => v.Url == videoUrl);

                string tagsString = string.Join(", ", video.AssociatedVideoTags.Select(avt => avt.Tag.Name));

                string uploadPath = Path.Combine("", "Thumbnails");
                string fileName = video.Thumbnail;
                string filePath = Path.Combine(uploadPath, fileName);
                FileInfo fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists)
                {
                    return NotFound("Thumbnail not found.");
                }

                byte[] thubnailBytes;

                using (FileStream fileStream = fileInfo.OpenRead())
                {
                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        fileStream.CopyTo(memoryStream);
                        thubnailBytes = memoryStream.ToArray();
                    }
                }

                string base64Thumbnail = Convert.ToBase64String(thubnailBytes);

                var videoDetailViewModel = new
                {
                    Title = video.Title,
                    Url = video.Url,
                    Description = video.Description,
                    Tags = tagsString,
                    Thumbnail = base64Thumbnail,
                    UploadedAt = video.CreatedAt
                };

                return Ok(videoDetailViewModel);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [Route("/video/{videoUrl}/links")]
        public IActionResult GetVideoLinks(string videoUrl)
        {
            try
            {
                //string secretKey = _configuration.GetSection("Key:SecretKey").Value;
                //var key = Encoding.UTF8.GetBytes(secretKey);

                //var validationParameters = new TokenValidationParameters
                //{
                //    ValidateIssuerSigningKey = true,
                //    IssuerSigningKey = new SymmetricSecurityKey(key),
                //    ValidateIssuer = false,
                //    ValidateAudience = false,
                //    ValidateLifetime = true,
                //    ClockSkew = TimeSpan.FromHours(24)
                //};

                //var authorizationHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
                //if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
                //{
                //    return Unauthorized();
                //}

                //var token = authorizationHeader.Substring("Bearer ".Length);

                //var tokenHandler = new JwtSecurityTokenHandler();

                //SecurityToken validatedToken;
                //var principal = tokenHandler.ValidateToken(token, validationParameters, out validatedToken);

                //var usernameClaim = principal.FindFirst("Username");
                //if (usernameClaim == null)
                //{
                //    return Unauthorized();
                //}

                //var username = usernameClaim.Value;

                //var user = this.Context.Users.FirstOrDefault(u => u.UserName == username);
                //if (user == null)
                //{
                //    return Unauthorized();
                //}

                var video = this.Context.Videos.FirstOrDefault(v => v.Url == videoUrl);
                if (video == null)
                {
                    return NotFound("Video doesn't exist.");
                }

                video = this.Context.Videos.Include(v => v.VideoHashes).FirstOrDefault(v => v.Url == videoUrl);
                var videoHashes = video.VideoHashes;
                if (videoHashes != null)
                {
                    List<object> VideoLinkItems = new List<object>();
                    foreach (VideoHash videoHash in videoHashes)
                    {
                        var videoLinkItemViewModel = new
                        {
                            Resolution = videoHash.Resolution,
                            Link = "https://ipfs.io/ipfs/" + videoHash.Hash
                        };
                        VideoLinkItems.Add(videoLinkItemViewModel);
                    }

                    return Ok(VideoLinkItems);
                }
                return NotFound("No video links found.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [Route("/video/{videoUrl}/tags")]
        public IActionResult GetVideoTags(string videoUrl)
        {
            try
            {
                string secretKey = _configuration.GetSection("Key:SecretKey").Value;
                var key = Encoding.UTF8.GetBytes(secretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromHours(24)
                };

                var authorizationHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
                {
                    return Unauthorized();
                }

                var token = authorizationHeader.Substring("Bearer ".Length);

                var tokenHandler = new JwtSecurityTokenHandler();

                SecurityToken validatedToken;
                var principal = tokenHandler.ValidateToken(token, validationParameters, out validatedToken);

                var usernameClaim = principal.FindFirst("Username");
                if (usernameClaim == null)
                {
                    return Unauthorized();
                }

                var username = usernameClaim.Value;

                var user = this.Context.Users.FirstOrDefault(u => u.UserName == username);
                if (user == null)
                {
                    return Unauthorized();
                }

                var video = this.Context.Videos.Include(v => v.AssociatedVideoTags).FirstOrDefault(v => v.Url == videoUrl);

                var videoTags = video.AssociatedVideoTags;

                if (videoTags != null)
                {
                    List<string> VideoTagItems = new List<string>();
                    foreach (var tag in  videoTags)
                    {
                        tag.Tag = this.Context.Tags.Find(tag.TagId);
                        string tagName = tag.Tag.Name;
                        VideoTagItems.Add(tagName);
                    }

                    return Ok(VideoTagItems);
                }

                return NotFound();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [Route("/video/{videoUrl}/likes-dislikes")]
        public IActionResult GetVideoLikesDislikes(string videoUrl)
        {
            try
            {
                string secretKey = _configuration.GetSection("Key:SecretKey").Value;
                var key = Encoding.UTF8.GetBytes(secretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromHours(24)
                };

                var authorizationHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
                {
                    return Unauthorized();
                }

                var token = authorizationHeader.Substring("Bearer ".Length);

                var tokenHandler = new JwtSecurityTokenHandler();

                SecurityToken validatedToken;
                var principal = tokenHandler.ValidateToken(token, validationParameters, out validatedToken);

                var usernameClaim = principal.FindFirst("Username");
                if (usernameClaim == null)
                {
                    return Unauthorized();
                }

                var username = usernameClaim.Value;

                var user = this.Context.Users.FirstOrDefault(u => u.UserName == username);
                if (user == null)
                {
                    return Unauthorized();
                }

                var video = this.Context.Videos.Include(v => v.VideoLikes).FirstOrDefault(v => v.Url == videoUrl);

                var VideoLikeDislikeViewModel = new
                {
                    LikesCount = video.VideoLikes.Where(vl => vl.LikeDislike == true).Count(),
                    DislikesCount = video.VideoLikes.Where(vl => vl.LikeDislike == false).Count(),
                    IsLiked = video.VideoLikes.Any(vl => vl.User == user && vl.LikeDislike == true),
                    IsDisliked = video.VideoLikes.Any(vl => vl.User == user && vl.LikeDislike == false)
                };

                return Ok(VideoLikeDislikeViewModel);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [Route("/video/{videoUrl}/details")]
        public IActionResult GetVideoDetails(string videoUrl)
        {
            try
            {
                string secretKey = _configuration.GetSection("Key:SecretKey").Value;
                var key = Encoding.UTF8.GetBytes(secretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromHours(24)
                };

                var authorizationHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
                {
                    return Unauthorized();
                }

                var token = authorizationHeader.Substring("Bearer ".Length);

                var tokenHandler = new JwtSecurityTokenHandler();

                SecurityToken validatedToken;
                var principal = tokenHandler.ValidateToken(token, validationParameters, out validatedToken);

                var usernameClaim = principal.FindFirst("Username");
                if (usernameClaim == null)
                {
                    return Unauthorized();
                }

                var username = usernameClaim.Value;

                var user = this.Context.Users.FirstOrDefault(u => u.UserName == username);
                if (user == null)
                {
                    return Unauthorized();
                }

                var video = this.Context.Videos.FirstOrDefault(v => v.Url == videoUrl);
                if (video == null)
                {
                    return NotFound("Video doesn't exist.");
                }

                video = this.Context.Videos.Include(v => v.Channel).FirstOrDefault(v => v.Url == videoUrl);
                user = this.Context.Users.Include(u => u.ManagingUserChannels).FirstOrDefault(u => u.UserName == username);

                string uploadPath = Path.Combine("", "Thumbnails");
                string fileName = video.Thumbnail;
                string filePath = Path.Combine(uploadPath, fileName);
                FileInfo fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists)
                {
                    return NotFound("Thumbnail not found.");
                }

                byte[] thubnailBytes;

                using (FileStream fileStream = fileInfo.OpenRead())
                {
                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        fileStream.CopyTo(memoryStream);
                        thubnailBytes = memoryStream.ToArray();
                    }
                }

                string base64Thumbnail = Convert.ToBase64String(thubnailBytes);

                var videoDetailViewModel = new
                {
                    Title = video.Title,
                    Url = video.Url,
                    Description = video.Description,
                    Thumbnail = base64Thumbnail,
                    UploadedAt = video.CreatedAt
                };

                return Ok(videoDetailViewModel);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [Route("/video/{videoUrl}/channelInfo")]
        public IActionResult GetVideoChannelInfo(string videoUrl)
        {
            try
            {
                string secretKey = _configuration.GetSection("Key:SecretKey").Value;
                var key = Encoding.UTF8.GetBytes(secretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromHours(24)
                };

                var authorizationHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
                {
                    return Unauthorized();
                }

                var token = authorizationHeader.Substring("Bearer ".Length);

                var tokenHandler = new JwtSecurityTokenHandler();

                SecurityToken validatedToken;
                var principal = tokenHandler.ValidateToken(token, validationParameters, out validatedToken);

                var usernameClaim = principal.FindFirst("Username");
                if (usernameClaim == null)
                {
                    return Unauthorized();
                }

                var username = usernameClaim.Value;

                var user = this.Context.Users.FirstOrDefault(u => u.UserName == username);
                if (user == null)
                {
                    return Unauthorized();
                }

                var video = this.Context.Videos.FirstOrDefault(v => v.Url == videoUrl);
                if (video == null)
                {
                    return NotFound("Video doesn't exist.");
                }

                video = this.Context.Videos.Include(v => v.Channel).FirstOrDefault(v => v.Url == videoUrl);
                user = this.Context.Users.Include(u => u.ManagingUserChannels).Include(u => u.FollowingUserChannels).FirstOrDefault(u => u.UserName == username);

                var channel = Context.Channels.FirstOrDefault(c => c.Id == video.Channel.Id);

                if (channel == null)
                {
                    return NotFound("Channel doesn't exist.");
                }

                string uploadPath = Path.Combine("", "Channels");
                string fileName = channel.Image;
                string filePath = Path.Combine(uploadPath, fileName);
                FileInfo fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists)
                {
                    return NotFound("Image not found.");
                }

                byte[] imageBytes;

                using (FileStream fileStream = fileInfo.OpenRead())
                {
                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        fileStream.CopyTo(memoryStream);
                        imageBytes = memoryStream.ToArray();
                    }
                }

                string base64Image = Convert.ToBase64String(imageBytes);

                var managedChannels = user.ManagingUserChannels;
                var isManagerOfChannel = managedChannels.Any(mc => mc.ChannelId == video.Channel.Id);
                var followedChannels = user.FollowingUserChannels;
                var isFollowerOfChannel = followedChannels.Any(fc => fc.ChannelId == video.Channel.Id);

                var channelItemViewModel = new
                {
                    Url = channel.Url,
                    Name = channel.Name,
                    Image = base64Image,
                    FollowersCount = channel.FollowingUserChannels.Count(),
                    VideosCount = channel.Videos.Count(),
                    IsManaged = isManagerOfChannel,
                    IsFollowed = isFollowerOfChannel
                };

                return Ok(channelItemViewModel);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [Route("/video/{videoUrl}/comments")]
        public IActionResult GetVideoComments(string videoUrl)
        {
            try
            {
                string secretKey = _configuration.GetSection("Key:SecretKey").Value;
                var key = Encoding.UTF8.GetBytes(secretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromHours(24)
                };

                var authorizationHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
                {
                    return Unauthorized();
                }

                var token = authorizationHeader.Substring("Bearer ".Length);

                var tokenHandler = new JwtSecurityTokenHandler();

                SecurityToken validatedToken;
                var principal = tokenHandler.ValidateToken(token, validationParameters, out validatedToken);

                var usernameClaim = principal.FindFirst("Username");
                if (usernameClaim == null)
                {
                    return Unauthorized();
                }

                var username = usernameClaim.Value;

                var user = this.Context.Users.FirstOrDefault(u => u.UserName == username);
                if (user == null)
                {
                    return Unauthorized();
                }

                var video = this.Context.Videos.FirstOrDefault(v => v.Url == videoUrl);
                if (video == null)
                {
                    return NotFound("Video doesn't exist.");
                }

                video = this.Context.Videos.Include(v => v.Comments).FirstOrDefault(v => v.Url == videoUrl);

                var videoComments = video.Comments;

                if (videoComments != null || videoComments.Any())
                {
                    videoComments = videoComments.OrderByDescending(c => c.CreatedAt).ToList();

                    List<object> VideoCommentItems = new List<object>();
                    foreach (Comment _comment in  videoComments)
                    {
                        Comment comment = this.Context.Comments.Include(c => c.User).FirstOrDefault(c => c.Id == _comment.Id);
                        string uploadPath = Path.Combine("", "Avatars");
                        string fileName = comment.User.Image;
                        string filePath = Path.Combine(uploadPath, fileName);
                        FileInfo fileInfo = new FileInfo(filePath);
                        if (!fileInfo.Exists)
                        {
                            return NotFound("Thumbnail not found.");
                        }

                        byte[] avatarBytes;
                        using (FileStream fileStream = fileInfo.OpenRead())
                        {
                            using (MemoryStream memoryStream = new MemoryStream())
                            {
                                fileStream.CopyTo(memoryStream);
                                avatarBytes = memoryStream.ToArray();
                            }
                        }

                        string base64Avatar = Convert.ToBase64String(avatarBytes);

                        User commentUser = comment.User;

                        comment = this.Context.Comments.Include(c => c.CommentLikes).FirstOrDefault(c => c.Id == _comment.Id);

                        int likesCount = comment.CommentLikes.Where(cl => cl.LikeDislike == true).Count();
                        int dislikesCount = comment.CommentLikes.Where(cl => cl.LikeDislike == false).Count();
                        bool isLiked = comment.CommentLikes.Any(cl => cl.User == user && cl.LikeDislike == true);
                        bool isDisliked = comment.CommentLikes.Any(cl => cl.User == user && cl.LikeDislike == false);

                        var commentItemModel = new
                        {
                            Id = comment.Id,
                            UserName = commentUser.UserName,
                            Description = comment.Description,
                            Avatar = base64Avatar,
                            LikesCount = likesCount,
                            DislikesCount = dislikesCount,
                            IsLiked = isLiked,
                            isDisliked = isDisliked
                        };

                        VideoCommentItems.Add(commentItemModel);
                    }

                    return Ok(VideoCommentItems);
                }

                return NotFound("No comments found in video.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut]
        [Route("/video/publish")]
        public IActionResult PublishVideo(string videoUrl)
        {
            try
            {
                string secretKey = _configuration.GetSection("Key:SecretKey").Value;
                var key = Encoding.UTF8.GetBytes(secretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromHours(24)
                };

                var authorizationHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
                {
                    return Unauthorized();
                }

                var token = authorizationHeader.Substring("Bearer ".Length);

                var tokenHandler = new JwtSecurityTokenHandler();

                SecurityToken validatedToken;
                var principal = tokenHandler.ValidateToken(token, validationParameters, out validatedToken);

                var usernameClaim = principal.FindFirst("Username");
                if (usernameClaim == null)
                {
                    return Unauthorized();
                }

                var username = usernameClaim.Value;

                var user = this.Context.Users.FirstOrDefault(u => u.UserName == username);
                if (user == null)
                {
                    return Unauthorized();
                }

                var video = this.Context.Videos.FirstOrDefault(v => v.Url == videoUrl);
                if (video == null)
                {
                    return NotFound("Video doesn't exist.");
                }

                video = this.Context.Videos.Include(v => v.Channel).FirstOrDefault(v => v.Url == videoUrl);
                user = this.Context.Users.Include(u => u.ManagingUserChannels).FirstOrDefault(u => u.UserName == username);

                var managedChannels = user.ManagingUserChannels;
                var isManagerOfChannel = managedChannels.Any(mc => mc.ChannelId == video.Channel.Id);

                if (!isManagerOfChannel)
                {
                    return Unauthorized();
                }

                if (video.IsPublished)
                {
                    video.IsPublished = false;
                    Context.Videos.Update(video);
                    Context.SaveChanges();
                    return Ok("Video will be unpublished.");
                }

                _encodingAndPublishingService.AddVideoUrl(video.Url);
                if (!(_encodingAndPublishingService.IsRunning))
                {
                    _encodingAndPublishingService.Start();
                }

                return Ok("Video will be published.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut]
        [Route("/video/edit")]
        public IActionResult EditVideo(VideoEditingDto videoEditingDto)
        {
            try
            {
                string secretKey = _configuration.GetSection("Key:SecretKey").Value;
                var key = Encoding.UTF8.GetBytes(secretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromHours(24)
                };

                var authorizationHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
                {
                    return Unauthorized();
                }

                var token = authorizationHeader.Substring("Bearer ".Length);

                var tokenHandler = new JwtSecurityTokenHandler();

                SecurityToken validatedToken;
                var principal = tokenHandler.ValidateToken(token, validationParameters, out validatedToken);

                var usernameClaim = principal.FindFirst("Username");
                if (usernameClaim == null)
                {
                    return Unauthorized();
                }

                var username = usernameClaim.Value;

                var user = this.Context.Users.FirstOrDefault(u => u.UserName == username);
                if (user == null)
                {
                    return Unauthorized();
                }

                var video = this.Context.Videos.FirstOrDefault(v => v.Url == videoEditingDto.VideoUrl);
                if (video == null)
                {
                    return NotFound("Video doesn't exist.");
                }

                video = this.Context.Videos.Include(v => v.Channel).FirstOrDefault(v => v.Url == videoEditingDto.VideoUrl);
                user = this.Context.Users.Include(u => u.ManagingUserChannels).FirstOrDefault(u => u.UserName == username);

                var managedChannels = user.ManagingUserChannels;
                var isManagerOfChannel = managedChannels.Any(mc => mc.ChannelId == video.Channel.Id);

                if (!isManagerOfChannel)
                {
                    return Unauthorized();
                }

                video.IsPublished = false;
                Context.Videos.Update(video);
                Context.SaveChanges();

                _videoEditingService.AddVideo(videoEditingDto);
                if (!(_videoEditingService.IsRunning))
                {
                    _videoEditingService.Start();
                }

                return Ok("Video will be applied edits.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut]
        [Route("/video/filter")]
        public IActionResult FilterVideo(VideoFilteringDto videoFilteringDto)
        {
            try
            {
                string secretKey = _configuration.GetSection("Key:SecretKey").Value;
                var key = Encoding.UTF8.GetBytes(secretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromHours(24)
                };

                var authorizationHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
                {
                    return Unauthorized();
                }

                var token = authorizationHeader.Substring("Bearer ".Length);

                var tokenHandler = new JwtSecurityTokenHandler();

                SecurityToken validatedToken;
                var principal = tokenHandler.ValidateToken(token, validationParameters, out validatedToken);

                var usernameClaim = principal.FindFirst("Username");
                if (usernameClaim == null)
                {
                    return Unauthorized();
                }

                var username = usernameClaim.Value;

                var user = this.Context.Users.FirstOrDefault(u => u.UserName == username);
                if (user == null)
                {
                    return Unauthorized();
                }

                var video = this.Context.Videos.FirstOrDefault(v => v.Url == videoFilteringDto.VideoUrl);
                if (video == null)
                {
                    return NotFound("Video doesn't exist.");
                }

                video = this.Context.Videos.Include(v => v.Channel).FirstOrDefault(v => v.Url == videoFilteringDto.VideoUrl);
                user = this.Context.Users.Include(u => u.ManagingUserChannels).FirstOrDefault(u => u.UserName == username);

                var managedChannels = user.ManagingUserChannels;
                var isManagerOfChannel = managedChannels.Any(mc => mc.ChannelId == video.Channel.Id);

                if (!isManagerOfChannel)
                {
                    return Unauthorized();
                }

                video.IsPublished = false;
                Context.Videos.Update(video);
                Context.SaveChanges();

                _videoFilteringService.AddVideo(videoFilteringDto);
                if (!(_videoFilteringService.IsRunning))
                {
                    _videoFilteringService.Start();
                }

                return Ok("Video will be applied filters.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        [Route("/video/like/")]
        public IActionResult LikeVideo(string videoUrl)
        {
            try
            {
                string secretKey = _configuration.GetSection("Key:SecretKey").Value;
                var key = Encoding.UTF8.GetBytes(secretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromHours(24)
                };

                var authorizationHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
                {
                    return Unauthorized();
                }

                var token = authorizationHeader.Substring("Bearer ".Length);

                var tokenHandler = new JwtSecurityTokenHandler();

                SecurityToken validatedToken;
                var principal = tokenHandler.ValidateToken(token, validationParameters, out validatedToken);

                var usernameClaim = principal.FindFirst("Username");
                if (usernameClaim == null)
                {
                    return Unauthorized();
                }

                var username = usernameClaim.Value;

                var user = this.Context.Users.FirstOrDefault(u => u.UserName == username);
                if (user == null)
                {
                    return Unauthorized();
                }

                var video = this.Context.Videos.Include(v => v.VideoLikes).FirstOrDefault(v => v.Url == videoUrl);

                if (video.VideoLikes.Any(vl => vl.User == user))
                {
                    var videoLikeDislike = video.VideoLikes.FirstOrDefault(vl => vl.User == user);
                    video.VideoLikes.Remove(videoLikeDislike);
                    if (videoLikeDislike.LikeDislike == false)
                    {
                        var newVideoLike = new VideoLike
                        {
                            User = user,
                            Video = video,
                            LikeDislike = true,
                            CreatedAt = DateTime.UtcNow,
                            LastUpdatedAt = DateTime.UtcNow
                        };
                        video.VideoLikes.Add(newVideoLike);
                    }
                    Context.Videos.Update(video);
                    Context.SaveChanges();
                }

                else
                {
                    var newVideoLike = new VideoLike
                    {
                        User = user,
                        Video = video,
                        LikeDislike = true,
                        CreatedAt = DateTime.UtcNow,
                        LastUpdatedAt = DateTime.UtcNow
                    };
                    video.VideoLikes.Add(newVideoLike);
                    Context.Videos.Update(video);
                    Context.SaveChanges();
                }

                video = this.Context.Videos.Include(v => v.VideoLikes).FirstOrDefault(v => v.Url == videoUrl);
                var VideoLikeDislikeViewModel = new
                {
                    LikesCount = video.VideoLikes.Where(vl => vl.LikeDislike == true).Count(),
                    DislikesCount = video.VideoLikes.Where(vl => vl.LikeDislike == false).Count(),
                    IsLiked = video.VideoLikes.Any(vl => vl.User == user && vl.LikeDislike == true),
                    IsDisliked = video.VideoLikes.Any(vl => vl.User == user && vl.LikeDislike == false)
                };

                return Ok(VideoLikeDislikeViewModel);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        [Route("/video/dislike/")]
        public IActionResult DislikeVideo(string videoUrl)
        {
            try
            {
                string secretKey = _configuration.GetSection("Key:SecretKey").Value;
                var key = Encoding.UTF8.GetBytes(secretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromHours(24)
                };

                var authorizationHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
                {
                    return Unauthorized();
                }

                var token = authorizationHeader.Substring("Bearer ".Length);

                var tokenHandler = new JwtSecurityTokenHandler();

                SecurityToken validatedToken;
                var principal = tokenHandler.ValidateToken(token, validationParameters, out validatedToken);

                var usernameClaim = principal.FindFirst("Username");
                if (usernameClaim == null)
                {
                    return Unauthorized();
                }

                var username = usernameClaim.Value;

                var user = this.Context.Users.FirstOrDefault(u => u.UserName == username);
                if (user == null)
                {
                    return Unauthorized();
                }

                var video = this.Context.Videos.Include(v => v.VideoLikes).FirstOrDefault(v => v.Url == videoUrl);

                if (video.VideoLikes.Any(vl => vl.User == user))
                {
                    var videoLikeDislike = video.VideoLikes.FirstOrDefault(vl => vl.User == user);
                    video.VideoLikes.Remove(videoLikeDislike);

                    if (videoLikeDislike.LikeDislike == true)
                    {
                        var newVideoDislike = new VideoLike
                        {
                            User = user,
                            Video = video,
                            LikeDislike = false,
                            CreatedAt = DateTime.UtcNow,
                            LastUpdatedAt = DateTime.UtcNow
                        };
                        video.VideoLikes.Add(newVideoDislike);
                    }
                    Context.Videos.Update(video);
                    Context.SaveChanges();
                }

                else
                {
                    var newVideoDislike = new VideoLike
                    {
                        User = user,
                        Video = video,
                        LikeDislike = false,
                        CreatedAt = DateTime.UtcNow,
                        LastUpdatedAt = DateTime.UtcNow
                    };
                    video.VideoLikes.Add(newVideoDislike);
                    Context.Videos.Update(video);
                    Context.SaveChanges();
                }

                video = this.Context.Videos.Include(v => v.VideoLikes).FirstOrDefault(v => v.Url == videoUrl);

                var VideoLikeDislikeViewModel = new
                {
                    LikesCount = video.VideoLikes.Where(vl => vl.LikeDislike == true).Count(),
                    DislikesCount = video.VideoLikes.Where(vl => vl.LikeDislike == false).Count(),
                    IsLiked = video.VideoLikes.Any(vl => vl.User == user && vl.LikeDislike == true),
                    IsDisliked = video.VideoLikes.Any(vl => vl.User == user && vl.LikeDislike == false)
                };

                return Ok(VideoLikeDislikeViewModel);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
