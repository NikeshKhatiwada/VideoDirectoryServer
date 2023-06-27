using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Security.Claims;
using BC = BCrypt.Net.BCrypt;
using VideoDirectory_Server.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using VideoDirectory_Server.Data;

namespace VideoDirectory_Server.Controllers
{
    public class AccountController : ControllerBase
    {
        private ApplicationDbContext? Context { get; }
        public AccountController(ApplicationDbContext context)
        {
            this.Context = context;
        }

        public HttpStatusCode Login(string username, string password)
        {
            // Get the user from the database.
            var user = GetUser(username);

            // If the user exists, authenticate them.
            if (user != null)
            {
                if (BC.Verify(password, user.Password))
                {
                    // Create a ClaimsPrincipal object for the authenticated user.
                    
                    var claimIdentity = new ClaimsIdentity(new[]
                        {
                            new Claim(ClaimTypes.Name, user.UserName)
                        }, CookieAuthenticationDefaults.AuthenticationScheme);
                    var principal = new ClaimsPrincipal(claimIdentity);
                    HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
                    HttpContext.Session.SetString("Username", user.UserName);

                    // Return a success message.
                    return HttpStatusCode.OK;
                }
            }

            // Return a failure message.
            return HttpStatusCode.Unauthorized;
        }

        private User GetUser(string username)
        {
            // TODO: Implement this method to get the user from the database.
            return this.Context.Users.Where(User => User.UserName == username).FirstOrDefault(); ;
        }
    }
}
