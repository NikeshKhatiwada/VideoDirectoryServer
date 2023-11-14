using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using VideoDirectory_Server.Data;
using VideoDirectory_Server.Dto.Message;
using VideoDirectory_Server.Models;

namespace VideoDirectory_Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class MessageController : ControllerBase
    {
        private ApplicationDbContext? Context { get; }
        private readonly IConfiguration _configuration;

        public MessageController(ApplicationDbContext context, IConfiguration configuration)
        {
            Context = context;
            _configuration = configuration;
        }

        [HttpPost]
        [Route("/message/add")]
        public IActionResult AddMessage(MessageInfoDto messageInfoDto)
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

                var chattingUser = this.Context.Users.FirstOrDefault(u => u.UserName == messageInfoDto.UserName);
                if (chattingUser == null)
                {
                    return NotFound("Username not found.");
                }

                if (messageInfoDto.Content.IsNullOrEmpty())
                {
                    return BadRequest("Empty message.");
                }

                var newMessage = new Message
                {
                    SenderId = user.Id,
                    ReceiverId = chattingUser.Id,
                    Content = messageInfoDto.Content,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow
                };

                Context.Messages.Add(newMessage);
                Context.SaveChanges();

                return Ok("Message successfully sent.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [Route("/messages/recent")]
        public IActionResult GetRecentMessages()
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

                var mostRecentMessages = Context.Messages
                    .Include(m => m.Sender)
                    .Include(m => m.Receiver)
                    .Where(m => m.SenderId == user.Id || m.ReceiverId == user.Id)
                    .GroupBy(m => m.SenderId == user.Id ? m.ReceiverId : m.SenderId)
                    .Select(g => g.OrderByDescending(m => m.CreatedAt).FirstOrDefault())
                    .OrderByDescending(m => m.CreatedAt)
                    .ToList();

                if (mostRecentMessages != null || mostRecentMessages.Any())
                {
                    List<object> RecentMessageItems = new List<object>();

                    foreach ( var message in mostRecentMessages )
                    {
                        string chatWithName = $"{message.Sender.FirstName} {message.Sender.LastName}";
                        string chatWithUserName = message.Sender.UserName;
                        string uploadPath = Path.Combine("", "Avatars");
                        string fileName = message.Sender.Image;
                        if (message.SenderId == user.Id)
                        {
                            chatWithName = $"{message.Receiver.FirstName} {message.Receiver.LastName}";
                            chatWithUserName = message.Receiver.UserName;
                            fileName = message.Receiver.Image;
                        }
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

                        var userItem = new
                        {
                            Name = chatWithName,
                            UserName = chatWithUserName,
                            Image = base64Image
                        };

                        var chatMessageItem = new
                        {
                            Sender = userItem,
                            Text = message.Content,
                            Time = message.CreatedAt.ToString("h:mm tt")
                        };

                        RecentMessageItems.Add(chatMessageItem);
                    }
                    return Ok(RecentMessageItems);
                }

                return NotFound("No messages found.");
            }

            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [Route("/messages/{userName}")]
        public IActionResult GetMessagesWithUser(string userName)
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

                var chattingUser = this.Context.Users.FirstOrDefault(u => u.UserName == userName);
                if (chattingUser == null)
                {
                    return NotFound();
                }

                var chatMessages = Context.Messages
                    .Include(m => m.Sender)
                    .Include(m => m.Receiver)
                    .Where(m => 
                        (m.SenderId == user.Id && m.ReceiverId == chattingUser.Id) || 
                        (m.SenderId == chattingUser.Id && m.ReceiverId == user.Id))
                    .OrderByDescending(m => m.CreatedAt)
                    .ToList();

                string chatWithName = $"{chattingUser.FirstName} {chattingUser.LastName}";
                string chatWithUserName = chattingUser.UserName;
                string uploadPath = Path.Combine("", "Avatars");
                string fileName = chattingUser.Image;
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

                var userItem = new
                {
                    Name = chatWithName,
                    UserName = chatWithUserName,
                    Image = base64Image
                };

                if (chatMessages != null || chatMessages.Any())
                {
                    List<object> ChatMessageItems = new List<object>();

                    foreach (var message in chatMessages)
                    {
                        var chatMessageItem = new
                        {
                            Sender = message.SenderId != user.Id ? userItem : null,
                            Text = message.Content,
                            Time = message.CreatedAt.ToString("h:mm tt")
                        };

                        ChatMessageItems.Add(chatMessageItem);
                    }

                    var responseObject = new
                    {
                        User = userItem,              
                        Messages = ChatMessageItems
                    };

                    return Ok(responseObject);
                }

                return NotFound("No messages found.");
            }

            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete]
        [Route("/messages/{userName}")]
        public IActionResult RemoveMessagesWithUser(string userName)
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

                var chattingUser = this.Context.Users.FirstOrDefault(u => u.UserName == userName);
                if (chattingUser == null)
                {
                    return NotFound();
                }

                var chatMessages = Context.Messages
                    .Where(m =>
                        (m.SenderId == user.Id && m.ReceiverId == chattingUser.Id) ||
                        (m.SenderId == chattingUser.Id && m.ReceiverId == user.Id))
                    .ToList();

                if (chatMessages != null || chatMessages.Any())
                {
                    foreach (var message in chatMessages)
                    {
                        Context.Messages.Remove(message);
                    }

                    Context.SaveChanges();

                    return Ok("Messages successfully removed.");
                }

                return NotFound("No messages found.");
            }

            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
