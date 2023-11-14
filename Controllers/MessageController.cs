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
    public class MessageController : ControllerBase
    {
        private ApplicationDbContext? Context { get; }
        private readonly IConfiguration _configuration;

        public MessageController(ApplicationDbContext context, IConfiguration configuration)
        {
            Context = context;
            _configuration = configuration;
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

                var mostRecentMessages = Context.Messages
                    .Include(m => m.Sender)
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
                        string uploadPath = Path.Combine("", "Avatars");
                        string fileName = message.Sender.Image;
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
                            Name = $"{message.Sender.FirstName} {message.Sender.LastName}",
                            Image = base64Image
                        };

                        var chatMessageItem = new
                        {
                            Sender = user,
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
    }
}
