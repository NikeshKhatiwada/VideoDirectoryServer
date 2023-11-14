using JWT;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using VideoDirectory_Server.Data;
using VideoDirectory_Server.Dto;

namespace VideoDirectory_Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class UserController : ControllerBase
    {
        private ApplicationDbContext? Context { get; }
        private readonly IConfiguration _configuration;
        //private readonly TokenValidationParameters _validationParameters;

        public UserController(ApplicationDbContext context, IConfiguration configuration/*, TokenValidationParameters validationParameters*/)
        {
            this.Context = context;
            //_validationParameters = validationParameters;
            _configuration = configuration;
        }

        [HttpGet]
        [Route("/user/profile")]
        public IActionResult GetUserProfile()
        {
            try
            {
                string secretKey = _configuration.GetSection("Key:SecretKey").Value;
                var key = Encoding.UTF8.GetBytes(secretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false, // Set to true if you want to validate the issuer
                    ValidateAudience = false, // Set to true if you want to validate the audience
                    ValidateLifetime = true, // Set to true if you want to validate the token lifetime (expiration)
                    ClockSkew = TimeSpan.FromHours(24) // Set the clock skew tolerance for token expiration validation
                };

                var authorizationHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
                {
                    return Unauthorized();
                }

                var token = authorizationHeader.Substring("Bearer ".Length);

                // Validate and decode the JWT token
                var tokenHandler = new JwtSecurityTokenHandler();

                SecurityToken validatedToken;
                var principal = tokenHandler.ValidateToken(token, validationParameters, out validatedToken);

                // Extract the username from the validated token
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
                string uploadPath = Path.Combine("", "Avatars");
                string fileName = user.Image;
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

                var userProfileViewModel = new
                {
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    UserName = user.UserName,
                    Email = user.Email,
                    Image = base64Image,
                    Password = user.Password,
                    CreatedAt = user.CreatedAt
                };

                return Ok(userProfileViewModel);
            }

            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut]
        [Route("/user/profile")]
        public IActionResult UpdateUserProfile(UserProfileDto userProfileDto)
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

                // Extract the username from the validated token
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

                // Update user profile properties
                user.FirstName = userProfileDto.FirstName;
                user.LastName = userProfileDto.LastName;
                user.UserName = userProfileDto.UserName;
                user.Email = userProfileDto.Email;
                user.LastUpdatedAt = DateTime.UtcNow;

                Context.Users.Update(user);
                Context.SaveChanges();

                //string uploadPath = Path.Combine("", "Avatars");
                //string fileName = user.Image;
                //string filePath = Path.Combine(uploadPath, fileName);
                //FileInfo fileInfo = new FileInfo(filePath);
                //if (!fileInfo.Exists)
                //{
                //    return NotFound("Image not found.");
                //}

                //byte[] imageBytes;

                //using (FileStream fileStream = fileInfo.OpenRead())
                //{
                //    using (MemoryStream memoryStream = new MemoryStream())
                //    {
                //        fileStream.CopyTo(memoryStream);
                //        imageBytes = memoryStream.ToArray();
                //    }
                //}

                //string base64Image = Convert.ToBase64String(imageBytes);

                //var userProfileViewModel = new
                //{
                //    FirstName = user.FirstName,
                //    LastName = user.LastName,
                //    UserName = user.UserName,
                //    Email = user.Email,
                //    Image = base64Image,
                //    Password = user.Password,
                //    CreatedAt = user.CreatedAt
                //};

                //return Ok(userProfileViewModel);

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut]
        [Route("/user/profile/image")]
        public IActionResult UpdateUserProfileImage(UserProfileImageDto userProfileImageDto)
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

                // Extract the username from the validated token
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

                if (userProfileImageDto.ImageBytes != null)
                {
                    string uploadPath = Path.Combine("", "Avatars");
                    string fileName = Path.GetRandomFileName();
                    string filePath = Path.Combine(uploadPath, fileName);
                    using (Stream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                    {
                        fileStream.Write(userProfileImageDto.ImageBytes, 0, userProfileImageDto.ImageBytes.Length);
                    }
                    FileInfo fileInfo = new FileInfo(filePath);
                    var fileExtension = userProfileImageDto.ImageExtension;
                    fileName = fileName.Substring(0, fileName.IndexOf("."));
                    fileName = fileName + fileExtension;
                    if (fileInfo.Exists)
                    {
                        fileInfo.MoveTo(Path.Combine(uploadPath, fileName));
                    }
                    user.Image = fileName;
                    user.LastUpdatedAt = DateTime.Now;
                }

                Context.Users.Update(user);
                Context.SaveChanges();

                //string imageFilePath = Path.Combine(Path.Combine("", "Avatars"), user.Image);
                //FileInfo imageFileInfo = new FileInfo(imageFilePath);
                //if (!imageFileInfo.Exists)
                //{
                //    return NotFound("Image not found.");
                //}

                //byte[] imageBytes;

                //using (FileStream fileStream = imageFileInfo.OpenRead())
                //{
                //    using (MemoryStream memoryStream = new MemoryStream())
                //    {
                //        fileStream.CopyTo(memoryStream);
                //        imageBytes = memoryStream.ToArray();
                //    }
                //}

                //string base64Image = Convert.ToBase64String(imageBytes);

                //var userProfileViewModel = new
                //{
                //    FirstName = user.FirstName,
                //    LastName = user.LastName,
                //    UserName = user.UserName,
                //    Email = user.Email,
                //    Image = base64Image,
                //    Password = user.Password,
                //    CreatedAt = user.CreatedAt
                ////};

                //return Ok(userProfileViewModel);

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [Route("/user/channels/managed")]
        public IActionResult GetManagedChannels()
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

                // Extract the username from the validated token
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

                var managedChannels = user.ManagingUserChannels;

                if (managedChannels != null)
                {
                    List<object> ManagedChannelItems = new List<object>();
                    foreach (var channel in managedChannels)
                    {
                        channel.Channel = this.Context.Channels
                            .Include(c => c.Videos)
                            .Include(c => c.FollowingUserChannels)
                            .Where(c => c.Id == channel.ChannelId)
                            .FirstOrDefault();
                        string uploadPath = Path.Combine("", "Channels");
                        string fileName = channel.Channel.Image;
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
                        var ManageChannelItemModel = new
                        {
                            Name = channel.Channel.Name,
                            Url = channel.Channel.Url,
                            Image = base64Image,
                            FollowersCount = channel.Channel.FollowingUserChannels.Count(),
                            VideosCount = channel.Channel.Videos.Count(),
                            Privilege = channel.Privilege
                        };
                        ManagedChannelItems.Add(ManageChannelItemModel);
                    }

                    return Ok(ManagedChannelItems);
                }

                return NotFound();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [Route("/user/channels/followed")]
        public IActionResult GetFollowedChannels()
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

                var followedChannels = user.FollowingUserChannels;

                if (followedChannels != null)
                {
                    List<object> FollowedChannelItems = new List<object>();
                    foreach (var channel in followedChannels)
                    {
                        channel.Channel = this.Context.Channels.Find(channel.ChannelId);
                        string uploadPath = Path.Combine("", "Channels");
                        string fileName = channel.Channel.Image;
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
                        var FollowedChannelItemModel = new
                        {
                            Name = channel.Channel.Name,
                            Url = channel.Channel.Url,
                            Image = base64Image,
                            FollowersCount = channel.Channel.FollowingUserChannels.Count()
                        };

                        FollowedChannelItems.Add(FollowedChannelItemModel);
                    }

                    return Ok(FollowedChannelItems);
                }

                return NotFound();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
