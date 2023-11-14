using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using VideoDirectory_Server.Data;

namespace VideoDirectory_Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SystemAdminUserController : ControllerBase
    {
        private ApplicationDbContext? Context { get; }
        private readonly IConfiguration _configuration;

        public SystemAdminUserController(ApplicationDbContext context, IConfiguration configuration)
        {
            Context = context;
            _configuration = configuration;
        }

        [HttpGet]
        [Route("/system-admin/users/all")]
        public IActionResult GetUsers()
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

                var users = this.Context.Users
                    .OrderByDescending(u => u.CreatedAt)
                    .ToList();

                List<object> Users = new List<object>();

                foreach ( var user in users )
                {
                    bool isSuspended = false;
                    if (user.SuspendedUntil != null)
                    {
                        if (user.SuspendedUntil >= DateTime.UtcNow)
                        {
                            isSuspended = true;
                        }
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

                    var userTableItem = new
                    {
                        UserName = user.UserName,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        Email = user.Email,
                        Image = base64Image,
                        IsSuspended = isSuspended
                    };

                    Users.Add(userTableItem);
                }
                return Ok(Users);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut]
        [Route("/system-admin/user/suspend")]
        public IActionResult SuspendOrUnsuspendUser(string user_name)
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

                var user = this.Context.Users
                    .FirstOrDefault(u => u.UserName == user_name);
                
                if (user == null)
                {
                    return NotFound("User not found.");
                }

                if (user.SuspendedUntil != null)
                {
                    if (user.SuspendedUntil <= DateTime.UtcNow)
                    {
                        user.SuspendedUntil = DateTime.UtcNow.AddDays(30);
                    }
                    user.SuspendedUntil = null;
                }
                else
                {
                    user.SuspendedUntil = DateTime.UtcNow.AddDays(30);
                }

                Context.Users.Update(user);
                Context.SaveChanges();

                return Ok("User suspended/unsuspended.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
