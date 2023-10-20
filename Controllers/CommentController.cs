using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using VideoDirectory_Server.Data;
using VideoDirectory_Server.Dto.Comment;
using VideoDirectory_Server.Models;

namespace VideoDirectory_Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CommentController : ControllerBase
    {
        private ApplicationDbContext? Context { get; }
        private readonly IConfiguration _configuration;

        public CommentController(ApplicationDbContext context, IConfiguration configuration)
        {
            this.Context = context;
            _configuration = configuration;
        }

        [HttpPost]
        [Route("/comment/add")]
        public IActionResult AddComment(CommentInfoDto commentInfoDto)
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

                var video = this.Context.Videos.FirstOrDefault(v => v.Url == commentInfoDto.VideoUrl && v.IsPublished == true);
                if (video  == null)
                {
                    return BadRequest("Video doesn't exist.");
                }

                var commentDescription = commentInfoDto.Description;

                if (commentDescription.IsNullOrEmpty())
                {
                    return BadRequest("Empty comment.");
                }

                var newComment = new Comment
                {
                    User = user,
                    Video = video,
                    Description = commentDescription,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow
                };

                Context.Comments.Add(newComment);
                Context.SaveChanges();

                return Ok("Comment posted successfully.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete]
        [Route("/comment/delete")]
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

                var user = this.Context.Users.FirstOrDefault(u => u.UserName == username);
                if (user == null)
                {
                    return Unauthorized();
                }

                var comment = this.Context.Comments.Include(c => c.User).FirstOrDefault(c => c.Id == commentId);

                if (comment == null)
                {
                    return NotFound();
                }

                if (comment.User != user)
                {
                    return BadRequest("Cannot delete others comment.");
                }

                Context.Comments.Remove(comment);
                Context.SaveChanges();

                return Ok("Comment deletion successful.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        [Route("/comment/like/")]
        public IActionResult LikeComment(int commentId)
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

                var comment = this.Context.Comments.Include(c => c.CommentLikes).FirstOrDefault(c => c.Id == commentId);

                if (comment == null)
                {
                    return NotFound();
                }

                if (comment.CommentLikes.Any(cl => cl.User == user))
                {
                    var commentLikeDislike = comment.CommentLikes.FirstOrDefault(cl => cl.User == user);
                    comment.CommentLikes.Remove(commentLikeDislike);

                    if (commentLikeDislike.LikeDislike == false)
                    {
                        var newCommentLike = new CommentLike
                        {
                            User = user,
                            Comment = comment,
                            LikeDislike = true,
                            CreatedAt = DateTime.UtcNow,
                            LastUpdatedAt = DateTime.UtcNow
                        };

                        comment.CommentLikes.Add(newCommentLike);
                    }
                    Context.Comments.Update(comment);
                    Context.SaveChanges();
                }

                else
                {
                    var newCommentLike = new CommentLike
                    {
                        User = user,
                        Comment = comment,
                        LikeDislike = true,
                        CreatedAt = DateTime.UtcNow,
                        LastUpdatedAt = DateTime.UtcNow
                    };

                    comment.CommentLikes.Add(newCommentLike);

                    Context.Comments.Update(comment);
                    Context.SaveChanges();
                }

                //var videoComments = this.Context.Comments.Where(c => c.Video == comment.Video);
                //List<object> VideoCommentItems = new List<object>();
                //foreach (var videoComment in videoComments)
                //{
                //    var VideoCommentItemModel = new
                //    {
                //        UserName = videoComment.User.UserName,
                //        Description = videoComment.Description,
                //        CreatedAt = videoComment.CreatedAt
                //    };
                //    VideoCommentItems.Add(VideoCommentItemModel);
                //}
                //return Ok(VideoCommentItems);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        [Route("/comment/dislike/")]
        public IActionResult DislikeComment(int commentId)
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

                var comment = this.Context.Comments.Include(c => c.CommentLikes).FirstOrDefault(c => c.Id == commentId);

                if (comment == null)
                {
                    return NotFound();
                }

                if (comment.CommentLikes.Any(cl => cl.User == user))
                {
                    var commentLikeDislike = comment.CommentLikes.FirstOrDefault(cl => cl.User == user);
                    comment.CommentLikes.Remove(commentLikeDislike);

                    if (commentLikeDislike.LikeDislike == true)
                    {
                        var newCommentLike = new CommentLike
                        {
                            User = user,
                            Comment = comment,
                            LikeDislike = false,
                            CreatedAt = DateTime.UtcNow,
                            LastUpdatedAt = DateTime.UtcNow
                        };

                        comment.CommentLikes.Add(newCommentLike);
                    }
                    Context.Comments.Update(comment);
                    Context.SaveChanges();
                }

                else
                {
                    var newCommentLike = new CommentLike
                    {
                        User = user,
                        Comment = comment,
                        LikeDislike = false,
                        CreatedAt = DateTime.UtcNow,
                        LastUpdatedAt = DateTime.UtcNow
                    };

                    comment.CommentLikes.Add(newCommentLike);

                    Context.Comments.Update(comment);
                    Context.SaveChanges();
                }

                //var videoComments = this.Context.Comments.Where(c => c.Video == comment.Video);
                //List<object> VideoCommentItems = new List<object>();
                //foreach (var videoComment in videoComments)
                //{
                //    var VideoCommentItemModel = new
                //    {
                //        UserName = videoComment.User.UserName,
                //        Description = videoComment.Description,
                //        CreatedAt = videoComment.CreatedAt
                //    };
                //    VideoCommentItems.Add(VideoCommentItemModel);
                //}
                //return Ok(VideoCommentItems);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
