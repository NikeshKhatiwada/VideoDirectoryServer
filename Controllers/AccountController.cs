using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Security.Claims;
using BC = BCrypt.Net.BCrypt;
using VideoDirectory_Server.Models;
using VideoDirectory_Server.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using VideoDirectory_Server.Data;
using Microsoft.AspNetCore.Authorization;
using VideoDirectory_Server.Dto;
using static System.Net.Mime.MediaTypeNames;
using System.Text.Json;

namespace VideoDirectory_Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AccountController : ControllerBase
    {
        private ApplicationDbContext? Context { get; }
        private readonly IConfiguration _configuration;
        private AuthTokenGenerator AuthTokenGenerator { get; }

        public AccountController(ApplicationDbContext context, IConfiguration configuration, AuthTokenGenerator authTokenGenerator)
        {
            this.Context = context;
            _configuration = configuration;
            AuthTokenGenerator = authTokenGenerator;
        }

        [HttpPost]
        [AllowAnonymous]
        [Route("/account/login")]
        public IActionResult Login(UserLoginDto userLoginDto)
        {
            // Get the user from the database.
            var user = GetUser(userLoginDto.UserName);

            // If the user exists, authenticate them.
            if (user != null)
            {
                if (BC.Verify(userLoginDto.Password, user.Password))
                {
                    // Create a ClaimsPrincipal object for the authenticated user.

                    //var claimIdentity = new ClaimsIdentity(new[]
                    //    {
                    //        new Claim(ClaimTypes.Name, user.UserName)
                    //    }, CookieAuthenticationDefaults.AuthenticationScheme);
                    //var principal = new ClaimsPrincipal(claimIdentity);
                    //await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
                    //HttpContext.Session.SetString("Username", user.UserName);

                    var secretKey = _configuration.GetValue<string>("Key:SecretKey");
                    var token = AuthTokenGenerator.GenerateAuthToken(user.UserName, secretKey);
                    return Ok(token);
                }
            }

            // Return a failure message.
            return Unauthorized();
        }

        [HttpPost]
        [AllowAnonymous]
        [Route("/account/register")]
        public IActionResult Register(UserRegistrationDto userRegistrationDto)
        {
            try
            {
                //using (var reader = new StreamReader(Request.Body))
                //{
                //    var json = reader.ReadToEnd();
                //    var userRegistrationDto = JsonSerializer.Deserialize<UserRegistrationDto>(json);

                // Check if the user already exists in the database.
                var existingUser = Context.Users.FirstOrDefault(u => u.UserName == userRegistrationDto.UserName);
                if (existingUser != null)
                {
                    // Return a failure message indicating that the user already exists.
                    return Conflict("User already exists.");
                }

                // Create a new user.
                var newUser = new User
                {
                    Id = Guid.NewGuid(),
                    FirstName = userRegistrationDto.FirstName,
                    LastName = userRegistrationDto.LastName,
                    UserName = userRegistrationDto.UserName,
                    Email = userRegistrationDto.Email,
                    Password = BC.HashPassword(userRegistrationDto.Password),
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow
                };

                if (userRegistrationDto.SelectedImage != null)
                {
                    string uploadPath = Path.Combine("", "Avatars");
                    string fileName = Path.GetRandomFileName();
                    string filePath = Path.Combine(uploadPath, fileName);
                    using (Stream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                    {
                        fileStream.Write(userRegistrationDto.SelectedImage, 0, userRegistrationDto.SelectedImage.Length);
                    }
                    FileInfo fileInfo = new FileInfo(filePath);
                    //var fileExtension = fileInfo.Extension;
                    var fileExtension = userRegistrationDto.SelectedImageExtension;
                    fileName = fileName.Substring(0, fileName.IndexOf("."));
                    fileName = fileName + fileExtension;
                    if (fileInfo.Exists)
                    {
                        fileInfo.MoveTo(Path.Combine(uploadPath, fileName));
                    }
                    newUser.Image = fileName;
                }

                // Save the user to the database.
                Context.Users.Add(newUser);
                Context.SaveChanges();

                // Return a success message.
                return Ok("Registration successful.");
                //}
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        //public IActionResult GetImage()
        //{
        //    var image = this.Context.Users.Where(User => User.UserName == HttpContext.Session.GetString("Username")).First().Image;
        //}

        [HttpGet]
        [AllowAnonymous]
        [Route("/hello")]
        public IActionResult Hello()
        {
            return Ok();
        }

        private User GetUser(string username)
        {
            // TODO: Implement this method to get the user from the database.
            return this.Context.Users.Where(User => User.UserName == username).FirstOrDefault();
        }
    }
}
