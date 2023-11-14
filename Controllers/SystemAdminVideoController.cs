using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using VideoDirectory_Server.Data;
using VideoDirectory_Server.Models;

namespace VideoDirectory_Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SystemAdminVideoController : ControllerBase
    {
        string videosFolder = "Videos";
        string thumbnailsFolder = "Thumbnails";

        private ApplicationDbContext? Context { get; }
        private readonly IConfiguration _configuration;

        public SystemAdminVideoController(ApplicationDbContext context, IConfiguration configuration)
        {
            Context = context;
            _configuration = configuration;
        }

        [HttpGet]
        [Route("/system-admin/videos/all")]
        public IActionResult GetVideos()
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

                var systemAdmin = this.Context.SystemAdmins.FirstOrDefault(sa => sa.Username == username);
                if (systemAdmin == null)
                {
                    return Unauthorized();
                }

                var videos = this.Context.Videos
                    .Include(v => v.Channel)
                    .OrderByDescending(u => u.CreatedAt)
                    .ToList();

                List<object> Videos = new List<object>();

                foreach (var video in videos)
                {
                    string uploadPath = Path.Combine("", "Thumbnails");
                    string fileName = video.Thumbnail;
                    string filePath = Path.Combine(uploadPath, fileName);
                    FileInfo fileInfo = new FileInfo(filePath);
                    if (!fileInfo.Exists)
                    {
                        return NotFound("Thumbnail not found.");
                    }

                    byte[] thumbnailBytes;

                    using (System.IO.FileStream fileStream = fileInfo.OpenRead())
                    {
                        using (MemoryStream memoryStream = new MemoryStream())
                        {
                            fileStream.CopyTo(memoryStream);
                            thumbnailBytes = memoryStream.ToArray();
                        }
                    }

                    string base64Thumbnail = Convert.ToBase64String(thumbnailBytes);

                    var videoTableItem = new
                    {
                        Id = video.Id,
                        Url = video.Url,
                        Thumbnail = base64Thumbnail,
                        Title = video.Title,
                        ChannelName = video.Channel.Name,
                        Description = video.Description
                    };

                    Videos.Add(videoTableItem);
                }
                return Ok(Videos);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut]
        [Route("/system-admin/video/delete")]
        public IActionResult DeleteVideo(int videoId)
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

                var systemAdmin = this.Context.SystemAdmins.FirstOrDefault(sa => sa.Username == username);
                if (systemAdmin == null)
                {
                    return Unauthorized();
                }

                var video = this.Context.Videos
                    .Include(v => v.VideoHashes)
                    .Include(v => v.AssociatedVideoTags)
                    .Include(v => v.VideoViews)
                    .Include(v => v.VideoLikes)
                    .Include(v => v.Comments)
                    .FirstOrDefault(v => v.Id == videoId);

                if (video == null)
                {
                    return NotFound("Video not found.");
                }

                if (video.Comments.Any())
                {
                    foreach (var videoComment in video.Comments)
                    {
                        var comment = this.Context.Comments
                            .Include(c => c.CommentLikes)
                            .FirstOrDefault(c => c.Id == videoComment.Id);

                        if (comment == null)
                        {
                            return NotFound("Comment not found.");
                        }

                        var commentReports = this.Context.CommentReports
                            .Where(r => r.CommentId == videoComment.Id);

                        if (commentReports.Any())
                        {
                            this.Context.CommentReports.RemoveRange(commentReports);
                            this.Context.SaveChanges();
                        }

                        if (comment.CommentLikes.Any())
                        {
                            comment.CommentLikes.Clear();
                            this.Context.Comments.Update(comment);
                            this.Context.SaveChanges();
                        }

                        Context.Comments.Remove(comment);
                        Context.SaveChanges();
                    }
                }

                var videoReports = this.Context.VideoReports
                            .Where(r => r.VideoId == video.Id);

                if (videoReports.Any())
                {
                    this.Context.VideoReports.RemoveRange(videoReports);
                    this.Context.SaveChanges();
                }

                if (video.VideoLikes.Any())
                {
                    video.VideoLikes.Clear();
                    this.Context.Videos.Update(video);
                    this.Context.SaveChanges();
                }

                if (video.VideoViews.Any())
                {
                    video.VideoViews.Clear();
                    this.Context.Videos.Update(video);
                    this.Context.SaveChanges();
                }

                if (video.AssociatedVideoTags.Any())
                {
                    video.AssociatedVideoTags.Clear();
                    this.Context.Videos.Update(video);
                    this.Context.SaveChanges();
                }

                if (video.VideoHashes.Any())
                {
                    RemoveIpfsHashes(video.VideoHashes.ToList());
                    video.VideoHashes.Clear();
                    this.Context.Videos.Update(video);
                    this.Context.SaveChanges();
                }

                var videoTranscript = this.Context.Transcripts
                    .Where(t => t.VideoId == video.Id)
                    .FirstOrDefault();

                if (videoTranscript != null)
                {
                    this.Context.Transcripts.Remove(videoTranscript);
                    this.Context.SaveChanges();
                }

                Context.Videos.Remove(video);
                Context.SaveChanges();

                var videoName = video.MainFilePath;

                string videoFilePath = Path.Combine(videosFolder, videoName);

                if (System.IO.File.Exists(videoFilePath))
                {
                    System.IO.File.Delete(videoFilePath);
                }

                var thumbnailName = video.Thumbnail;

                string thumbnailFilePath = Path.Combine(thumbnailsFolder, thumbnailName);

                if (System.IO.File.Exists(thumbnailFilePath))
                {
                    System.IO.File.Delete(thumbnailFilePath);
                }

                return Ok("Video deleted.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task RemoveIpfsHashes(List<VideoHash> videoHashes)
        {
            foreach (var videoHash in videoHashes)
            {
                await UnpinFromIPFS(videoHash.Hash);
            }
            await RunIPFSGarbageCollection();
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        private async Task UnpinFromIPFS(string ipfsHash)
        {
            using (var client = new HttpClient())
            {
                string apiEndpoint = "http://localhost:5001/api/v0/";

                var response = await client.PostAsync(apiEndpoint + "pin/rm" + $"?arg={ipfsHash}", null);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("Video unpinned successfully.");
                }
                else
                {
                    throw new Exception($"Failed to unpin from IPFS Desktop. StatusCode: {response.StatusCode}");
                }
            }
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        private async Task RunIPFSGarbageCollection()
        {
            using (var client = new HttpClient())
            {
                string apiEndpoint = "http://localhost:5001/api/v0/";

                var response = await client.PostAsync(apiEndpoint + "repo/gc", null);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("Unpinned File(s) removed.");
                }
                else
                {
                    throw new Exception($"Failed to remove from IPFS Desktop. StatusCode: {response.StatusCode}");
                }
            }
        }
    }
}
