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
    public class SystemAdminCommentController : ControllerBase
    {
        private ApplicationDbContext? Context { get; }
        private readonly IConfiguration _configuration;

        public SystemAdminCommentController(ApplicationDbContext context, IConfiguration configuration)
        {
            Context = context;
            _configuration = configuration;
        }

        [HttpGet]
        [Route("/system-admin/comments/all")]
        public IActionResult GetComments()
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

                var comments = this.Context.Comments
                    .Include(c => c.User)
                    .Include(c => c.Video)
                    .OrderByDescending(u => u.CreatedAt)
                    .ToList();

                List<object> Comments = new List<object>();

                foreach (var comment in comments)
                {
                    var commentTableItem = new
                    {
                        Id = comment.Id,
                        UserName = comment.User.Id.ToString(),
                        VideoId = comment.Video.Id.ToString(),
                        Description = comment.Description
                    };

                    Comments.Add(commentTableItem);
                }
                return Ok(Comments);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut]
        [Route("/system-admin/comment/delete")]
        public IActionResult DeleteComment(int commentId)
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

                var comment = this.Context.Comments
                    .Include(c => c.CommentLikes)
                    .FirstOrDefault(c => c.Id == commentId);

                if (comment == null)
                {
                    return NotFound("Comment not found.");
                }

                var commentReports = this.Context.CommentReports
                    .Where(c => c.Id == commentId);

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

                return Ok("Comment deleted.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
