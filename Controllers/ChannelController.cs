using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using VideoDirectory_Server.Data;
using VideoDirectory_Server.Dto;
using VideoDirectory_Server.Dto.Channel;
using VideoDirectory_Server.Models;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace VideoDirectory_Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ChannelController : ControllerBase
    {
        private ApplicationDbContext? Context { get; }
        private readonly IConfiguration _configuration;

        public ChannelController(ApplicationDbContext context, IConfiguration configuration)
        {
            this.Context = context;
            _configuration = configuration;
        }

        [HttpPost]
        [Route("/channel/create")]
        public IActionResult CreateChannel(ChannelDetailDto channelDetailDto)
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

                var existingChannel = Context.Channels.FirstOrDefault(u => u.Url == channelDetailDto.Url);
                if (existingChannel != null)
                {
                    return Conflict("Channel Url already used.");
                }

                var newChannel = new Channel
                {
                    Id = Guid.NewGuid(),
                    Url = channelDetailDto.Url,
                    Name = channelDetailDto.Name,
                    Description = channelDetailDto.Description,
                    SiteLink = channelDetailDto.SiteLink,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow
                };

                if (channelDetailDto.Image != null)
                {
                    byte[] imageData = Convert.FromBase64String(channelDetailDto.Image);
                    string uploadPath = Path.Combine("", "Channels");
                    string fileName = Path.GetRandomFileName();
                    string filePath = Path.Combine(uploadPath, fileName);
                    using (Stream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                    {
                        fileStream.Write(imageData, 0, imageData.Length);
                    }
                    FileInfo fileInfo = new FileInfo(filePath);
                    var fileExtension = channelDetailDto.ImageExtension;
                    fileName = fileName.Substring(0, fileName.IndexOf("."));
                    fileName = fileName + fileExtension;
                    if (fileInfo.Exists)
                    {
                        fileInfo.MoveTo(Path.Combine(uploadPath, fileName));
                    }
                    newChannel.Image = fileName;
                }

                Context.Channels.Add(newChannel);
                Context.SaveChanges();

                var newlyAddedChannel = Context.Channels.Where(c => c.Url == newChannel.Url).FirstOrDefault();

                var newManagingUserChannel = new ManagingUserChannel
                {
                    UserId = user.Id,
                    ChannelId = newlyAddedChannel.Id,
                    Privilege = 'O',
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow
                };

                newlyAddedChannel.ManagingUserChannels.Add(newManagingUserChannel);
                Context.SaveChanges();

                return Ok(newChannel.Url);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [Route("/channels/search")]
        public IActionResult GetSearchedChannels(string value)
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

                if (string.IsNullOrEmpty(value))
                {
                    return BadRequest("Search query is empty.");
                }

                var searchedChannels = this.Context.Channels
                    .Where(c => EF.Functions.ILike(c.Name, $"%{value}%") || EF.Functions.ILike(c.Description, $"%{value}%"))
                    .ToList();

                if (searchedChannels != null)
                {
                    var managedChannels = user.ManagingUserChannels;
                    var followedChannels = user.FollowingUserChannels;
                    List<object> SearchedChannelItems = new List<object>();
                    foreach (var channel1 in searchedChannels)
                    {
                        bool isChannelManager = false;
                        var isManagerOfChannel = managedChannels.Any(mc => mc.ChannelId == channel1.Id);

                        if (isManagerOfChannel)
                        {
                            isChannelManager = true;
                        }

                        bool isChannelFollowed = false;
                        var isFollowerOfChannel = followedChannels.Any(fc => fc.ChannelId == channel1.Id);
                        
                        if (isFollowerOfChannel)
                        {
                            isChannelFollowed = true;
                        }

                        var channel = this.Context.Channels
                            .Include(c => c.FollowingUserChannels)
                            .Include(c => c.Videos)
                            .FirstOrDefault(c => c.Id == channel1.Id);
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
                        var searchedChannelItemModel = new
                        {
                            Url = channel.Url,
                            Name = channel.Name,
                            Image = base64Image,
                            FollowersCount = channel.FollowingUserChannels.Count(),
                            VideosCount = channel.Videos.Count(),
                            IsManaged = isChannelManager,
                            IsFollowed = isChannelFollowed
                        };
                        SearchedChannelItems.Add(searchedChannelItemModel);
                    }

                    return Ok(SearchedChannelItems);
                }

                return NotFound();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [Route("/channel/{channelUrl}/managed/details")]
        public IActionResult GetManagedChannelDetails(string channelUrl)
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

                var channel = this.Context.Channels.FirstOrDefault(c => c.Url == channelUrl);

                if (channel != null)
                {
                    var managedChannels = user.ManagingUserChannels;
                    var isManagerOfChannel = managedChannels.Any(mc => mc.ChannelId == channel.Id);

                    if (!isManagerOfChannel)
                    {
                        return Unauthorized();
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

                    var managedChannelDetailViewModel = new
                    {
                        Url = channel.Url,
                        Name = channel.Name,
                        Image = base64Image,
                        Description = channel.Description,
                        SiteLink = channel.SiteLink,
                        CreatedAt = channel.CreatedAt,
                        MyUserName = user.UserName
                    };

                    return Ok(managedChannelDetailViewModel);
                }

                return NotFound("Channel doesn't exist.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut]
        [Route("/channel/{channelUrl}/managed/details")]
        public IActionResult UpdateManagedChannelDetails(EditChannelDetailDto channelDetailDto, string channelUrl)
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

                var channel = Context.Channels.FirstOrDefault(c => c.Url == channelUrl);

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

                channel.Name = channelDetailDto.Name;
                channel.Description = channelDetailDto.Description;

                if (!string.IsNullOrWhiteSpace(channelDetailDto.SiteLink))
                {
                    channel.SiteLink = channelDetailDto.SiteLink;
                }

                channel.LastUpdatedAt = DateTime.UtcNow;

                Context.Channels.Update(channel);
                Context.SaveChanges();

                return Ok("Channel detail update successful.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut]
        [Route("/channel/managed/details/image")]
        public IActionResult UpdateManagedChannelImage(ChannelDetailImageDto channelDetailImageDto)
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

                var channel = Context.Channels.FirstOrDefault(u => u.Url == channelDetailImageDto.Url);

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

                if (channelDetailImageDto.Image != null)
                {
                    byte[] imageData = Convert.FromBase64String(channelDetailImageDto.Image);
                    string uploadPath = Path.Combine("", "Channels");
                    string fileName = Path.GetRandomFileName();
                    string filePath = Path.Combine(uploadPath, fileName);
                    using (Stream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                    {
                        fileStream.Write(imageData, 0, imageData.Length);
                    }
                    FileInfo fileInfo = new FileInfo(filePath);
                    var fileExtension = channelDetailImageDto.ImageExtension;
                    fileName = fileName.Substring(0, fileName.IndexOf("."));
                    fileName = fileName + fileExtension;
                    if (fileInfo.Exists)
                    {
                        fileInfo.MoveTo(Path.Combine(uploadPath, fileName));
                    }
                    channel.Image = fileName;
                    channel.LastUpdatedAt = DateTime.Now;
                }

                Context.Channels.Update(channel);
                Context.SaveChanges();

                return Ok("Channel image update successful.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        [Route("/channel/manager")]
        public IActionResult AddChannelManager(ChannelManagerDto channelManagerDto)
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

                var channel = Context.Channels.FirstOrDefault(u => u.Url == channelManagerDto.ChannelUrl);

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

                var managerUser = Context.Users.FirstOrDefault(u => u.UserName == channelManagerDto.UserName);
                if (managerUser == null)
                {
                    return NotFound("User doesn't exist.");
                }

                var newChannelManager = new ManagingUserChannel
                {
                    Id = Guid.NewGuid(),
                    UserId = managerUser.Id,
                    ChannelId = channel.Id,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow
                };

                channel.ManagingUserChannels.Add(newChannelManager);
                Context.Update(channel);
                Context.SaveChanges();

                return Ok("Manager added successfully.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [Route("/channel/{channelUrl}/managed/videos")]
        public IActionResult GetManagedChannelVideos(string channelUrl)
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

                var channel = this.Context.Channels.FirstOrDefault(c => c.Url == channelUrl);

                if (channel != null)
                {
                    var managedChannels = user.ManagingUserChannels;
                    var isManagerOfChannel = managedChannels.Any(mc => mc.ChannelId == channel.Id);

                    if (!isManagerOfChannel)
                    {
                        return Unauthorized();
                    }

                    channel = this.Context.Channels.Include(c => c.Videos).FirstOrDefault(c => c.Url == channelUrl);
                    var managedChannelVideos = channel.Videos;

                    if (managedChannelVideos != null)
                    {
                        List<object> ManagedChannelVideoItems = new List<object>();
                        foreach (Video video in managedChannelVideos)
                        {
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

                            var managedChannelVideoItemModel = new
                            {
                                Title = video.Title,
                                Url = video.Url,
                                Thumbnail = base64Thumbnail,
                                PublishStatus = video.IsPublished,
                                CreatedAt = video.CreatedAt
                            };

                            ManagedChannelVideoItems.Add(managedChannelVideoItemModel);
                        }

                        return Ok(ManagedChannelVideoItems);
                    }

                    return NotFound("No videos found in channel.");
                }

                return NotFound("Channel doesn't exist.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [Route("/channel/{channelUrl}/managed/managers")]
        public IActionResult GetManagedChannelManagers(string channelUrl)
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

                var channel = this.Context.Channels.FirstOrDefault(c => c.Url == channelUrl);

                if (channel != null)
                {
                    var managedChannels = user.ManagingUserChannels;
                    var isManagerOfChannel = managedChannels.Any(mc => mc.ChannelId == channel.Id);

                    if (!isManagerOfChannel)
                    {
                        return Unauthorized();
                    }

                    channel = this.Context.Channels.Include(c => c.ManagingUserChannels).FirstOrDefault(c => c.Url == channelUrl);
                    var managedChannelManagers = channel.ManagingUserChannels;

                    if (managedChannelManagers != null)
                    {
                        List<object> ChannelManagerItems = new List<object>();
                        foreach (ManagingUserChannel managingUserChannel in managedChannelManagers)
                        {
                            string uploadPath = Path.Combine("", "Avatars");
                            string fileName = managingUserChannel.User.Image;
                            string filePath = Path.Combine(uploadPath, fileName);
                            FileInfo fileInfo = new FileInfo(filePath);
                            if (!fileInfo.Exists)
                            {
                                return NotFound("User image not found.");
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

                            var channelManagerItemModel = new
                            {
                                Name = user.FirstName + user.LastName,
                                UserName = user.UserName,
                                Image = base64Image,
                                Privilege = managingUserChannel.Privilege
                            };

                            ChannelManagerItems.Add(channelManagerItemModel);
                        }

                        return Ok(ChannelManagerItems);
                    }

                    return NotFound("No managers found in channel.");
                }

                return NotFound("Channel doesn't exist.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        [Route("/channel/managed/managers")]
        public IActionResult AddManagedChannelManager(ChannelManagerDto channelManagerDto)
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

                var channel = this.Context.Channels.FirstOrDefault(c => c.Url == channelManagerDto.ChannelUrl);

                var managingUser = this.Context.Users.FirstOrDefault(u => u.UserName == channelManagerDto.UserName);

                if (channel == null)
                {
                    return NotFound("Channel doesn't exist.");
                }

                if (managingUser == null)
                {
                    return NotFound("User doesn't exist.");
                }

                var managedChannels = user.ManagingUserChannels;
                var isManagerOfChannel = managedChannels.Any(mc => mc.ChannelId == channel.Id);

                if (!isManagerOfChannel)
                {
                    return Unauthorized();
                }

                var isOwnerOfChannel = managedChannels.Any(mc => mc.ChannelId == channel.Id && mc.Privilege == 'O');

                if (!isOwnerOfChannel)
                {
                    return Unauthorized();
                }

                channel = this.Context.Channels.Include(c => c.ManagingUserChannels).FirstOrDefault(c => c.Url == channelManagerDto.ChannelUrl);
                var managedChannelManagers = channel.ManagingUserChannels;

                if (managedChannelManagers.Any(mc => mc.UserId == managingUser.Id))
                {
                    return Conflict("User is already manager.");
                }

                var newManagingUserChannel = new ManagingUserChannel
                {
                    UserId = managingUser.Id,
                    ChannelId = channel.Id,
                    Privilege = 'A',
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow
                };

                channel.ManagingUserChannels.Add(newManagingUserChannel);
                Context.SaveChanges();

                return Ok("Channel admin added.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut]
        [Route("/channel/managed/managers")]
        public IActionResult UpdateManagedChannelManager(ChannelManagerDto channelManagerDto)
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

                var channel = this.Context.Channels.FirstOrDefault(c => c.Url == channelManagerDto.ChannelUrl);

                var managingUser = this.Context.Users.FirstOrDefault(u => u.UserName == channelManagerDto.UserName);

                if (channel == null)
                {
                    return NotFound("Channel doesn't exist.");
                }

                if (managingUser == null)
                {
                    return NotFound("User doesn't exist.");
                }

                var managedChannels = user.ManagingUserChannels;
                var isManagerOfChannel = managedChannels.Any(mc => mc.ChannelId == channel.Id);

                if (!isManagerOfChannel)
                {
                    return Unauthorized();
                }

                var isOwnerOfChannel = managedChannels.Any(mc => mc.ChannelId ==  channel.Id && mc.Privilege == 'O');

                if (!isOwnerOfChannel)
                {
                    return Unauthorized();
                }

                channel = this.Context.Channels.Include(c => c.ManagingUserChannels).FirstOrDefault(c => c.Url == channelManagerDto.ChannelUrl);
                var managedChannelManagers = channel.ManagingUserChannels;

                if (!(managedChannelManagers.Any(mc => mc.UserId == managingUser.Id && mc.Privilege == 'A')))
                {
                    return BadRequest("User cannot be made admin.");
                }

                var managingUserChannel = channel.ManagingUserChannels.FirstOrDefault(mc => mc.UserId == managingUser.Id);

                channel.ManagingUserChannels.Remove(managingUserChannel);
                Context.Channels.Update(channel);

                managingUserChannel.Privilege = 'O';
                managingUserChannel.LastUpdatedAt = DateTime.UtcNow;

                channel.ManagingUserChannels.Add(managingUserChannel);
                Context.Channels.Update(channel);
                Context.SaveChanges();

                return Ok("Channel admin updated to owner.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete]
        [Route("/channel/managed/managers")]
        public IActionResult RemoveManagedChannelManager([FromQuery] ChannelManagerDto channelManagerDto)
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

                var channel = this.Context.Channels.FirstOrDefault(c => c.Url == channelManagerDto.ChannelUrl);

                var managingUser = this.Context.Users.FirstOrDefault(u => u.UserName == channelManagerDto.UserName);

                if (channel == null)
                {
                    return NotFound("Channel doesn't exist.");
                }

                if (managingUser == null)
                {
                    return NotFound("User doesn't exist.");
                }

                var managedChannels = user.ManagingUserChannels;
                var isManagerOfChannel = managedChannels.Any(mc => mc.ChannelId == channel.Id);

                if (!isManagerOfChannel)
                {
                    return Unauthorized();
                }

                var isOwnerOfChannel = managedChannels.Any(mc => mc.ChannelId == channel.Id && mc.Privilege == 'O');

                if (!isOwnerOfChannel)
                {
                    return Unauthorized();
                }

                channel = this.Context.Channels.Include(c => c.ManagingUserChannels).FirstOrDefault(c => c.Url == channelManagerDto.ChannelUrl);
                var managedChannelManagers = channel.ManagingUserChannels;

                if (!(managedChannelManagers.Any(mc => mc.UserId == managingUser.Id && mc.Privilege == 'A')))
                {
                    return BadRequest("User cannot be removed.");
                }

                var managingUserChannel = channel.ManagingUserChannels.FirstOrDefault(mc => mc.UserId == managingUser.Id);

                channel.ManagingUserChannels.Remove(managingUserChannel);
                Context.Channels.Update(channel);

                Context.SaveChanges();

                return Ok("Channel admin removed.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [Route("/channel/{channelUrl}/details")]
        public IActionResult GetChannelDetails(string channelUrl)
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

                user = this.Context.Users.Include(u => u.FollowingUserChannels).FirstOrDefault(u => u.UserName == username);

                var channel = this.Context.Channels.Include(c => c.FollowingUserChannels).FirstOrDefault(c => c.Url == channelUrl);

                if (channel != null)
                {
                    bool isFollowed = user.FollowingUserChannels.Any(c => c.ChannelId == channel.Id);
                    int videosCount = this.Context.Videos.Where(v => v.Channel == channel).Count();

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

                    var channelDetailViewModel = new
                    {
                        Url = channel.Url,
                        Name = channel.Name,
                        Image = base64Image,
                        IsFollowed = isFollowed,
                        FollowersCount = channel.FollowingUserChannels.Count(),
                        VideosCount = videosCount,
                        Description = channel.Description,
                        SiteLink = channel.SiteLink ?? " ",
                        CreatedAt = channel.CreatedAt
                    };

                    return Ok(channelDetailViewModel);
                }

                return NotFound("Channel doesn't exist.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [Route("/channel/{channelUrl}/videos")]
        public IActionResult GetChannelVideos(string channelUrl)
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

                var channel = this.Context.Channels.FirstOrDefault(c => c.Url == channelUrl);

                if (channel != null)
                {
                    channel = this.Context.Channels.Include(c => c.Videos).FirstOrDefault(c => c.Url == channelUrl);
                    var channelVideos = channel.Videos.Where(v => v.IsPublished == true);

                    if (channelVideos != null)
                    {
                        List<object> channelVideoItems = new List<object>();

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

                        foreach (Video video in channelVideos)
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


                            var channelVideoItemModel = new
                            {
                                Thumbnail = base64Thumbnail,
                                VideoUrl = video.Url,
                                Title = video.Title,
                                ChannelName = channel.Name,
                                ChannelImage = base64Image,
                                ChannelUrl = channel.Url
                            };

                            channelVideoItems.Add(channelVideoItemModel);
                        }

                        return Ok(channelVideoItems);
                    }

                    return NotFound("No videos found in channel.");
                }

                return NotFound("Channel doesn't exist.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        [Route("/channel/follow-unfollow")]
        public IActionResult FollowOrUnfollowChannel(string channelUrl)
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

                var channel = Context.Channels.FirstOrDefault(c => c.Url == channelUrl);

                if (channel == null)
                {
                    return NotFound("Channel doesn't exist.");
                }

                channel = Context.Channels.Include(c => c.FollowingUserChannels).FirstOrDefault(c => c.Url == channelUrl);

                if (!(channel.FollowingUserChannels.Any(c => c.UserId == user.Id)))
                {
                    var newChannelFollowing = new FollowingUserChannel
                    {
                        UserId = user.Id,
                        ChannelId = channel.Id,
                        CreatedAt = DateTime.UtcNow,
                        LastUpdatedAt = DateTime.UtcNow
                    };
                    channel.FollowingUserChannels.Add(newChannelFollowing);
                    Context.Channels.Update(channel);
                    Context.SaveChanges();
                    return Ok("Following Successful.");
                }

                var existingChannelFollowing = channel.FollowingUserChannels.FirstOrDefault(c => c.UserId == user.Id);
                channel.FollowingUserChannels.Remove(existingChannelFollowing);
                Context.Channels.Update(channel);
                Context.SaveChanges();
                return Ok("Unfollowing Successful.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
