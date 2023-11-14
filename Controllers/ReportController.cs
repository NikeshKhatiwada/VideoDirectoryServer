using FM.LiveSwitch.Matroska;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using VideoDirectory_Server.Data;
using VideoDirectory_Server.Dto.Comment;
using VideoDirectory_Server.Dto.Report;
using VideoDirectory_Server.Models;
using VideoDirectory_Server.Services;

namespace VideoDirectory_Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ReportController : ControllerBase
    {
        private ApplicationDbContext? Context { get; }
        private readonly IConfiguration _configuration;

        public ReportController(ApplicationDbContext context, IConfiguration configuration)
        {
            this.Context = context;
            _configuration = configuration;
        }

        [HttpPost]
        [Route("/report/add")]
        public IActionResult AddReport(ReportInfoDto reportInfoDto)
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

                if (reportInfoDto.Reason.IsNullOrEmpty())
                {
                    return BadRequest("Empty report.");
                }

                if (reportInfoDto.ContentType == 'V')
                {
                    var video = this.Context.Videos.FirstOrDefault(v => v.Url == reportInfoDto.ContentIdentifier);
                    if (video == null)
                    {
                        return BadRequest("Video doesn't exist.");
                    }

                    var newVideoReport = new VideoReport
                    {
                        ReporterUser = user,
                        Reason = reportInfoDto.Reason,
                        VideoId = video.Id,
                        CreatedAt = DateTime.UtcNow,
                        LastUpdatedAt = DateTime.UtcNow
                    };

                    Context.VideoReports.Add(newVideoReport);
                    Context.SaveChanges();

                    return Ok("Video reported successfully.");
                }

                if (reportInfoDto.ContentType == 'C')
                {
                    var comment = this.Context.Comments.FirstOrDefault(c => c.Id.ToString() == reportInfoDto.ContentIdentifier);
                    if (comment == null)
                    {
                        return BadRequest("Comment doesn't exist.");
                    }

                    var newCommentReport = new CommentReport
                    {
                        ReporterUser = user,
                        Reason = reportInfoDto.Reason,
                        CommentId = comment.Id,
                        CreatedAt = DateTime.UtcNow,
                        LastUpdatedAt = DateTime.UtcNow
                    };

                    Context.CommentReports.Add(newCommentReport);
                    Context.SaveChanges();

                    return Ok("Comment reported successfully.");
                }

                if (reportInfoDto.ContentType == 'K')
                {
                    var channel = this.Context.Channels.FirstOrDefault(c => c.Url == reportInfoDto.ContentIdentifier);
                    if (channel == null)
                    {
                        return BadRequest("Channel doesn't exist.");
                    }

                    var newChannelReport = new ChannelReport
                    {
                        ReporterUser = user,
                        Reason = reportInfoDto.Reason,
                        ChannelId = channel.Id,
                        CreatedAt = DateTime.UtcNow,
                        LastUpdatedAt = DateTime.UtcNow
                    };

                    Context.ChannelReports.Add(newChannelReport);
                    Context.SaveChanges();

                    return Ok("Channel reported successfully.");
                }

                return BadRequest("Invalid request type.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
