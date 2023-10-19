using JWT.Builder;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace VideoDirectory_Server.Services
{
    public class AuthTokenGenerator
    {
        public string GenerateAuthToken(string UserName, string secretKey)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(secretKey);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim("exp", DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeSeconds().ToString()),
                    new Claim("Username", UserName)
            }),
                Expires = DateTimeOffset.UtcNow.AddDays(7).DateTime,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            return tokenString;
        }
    }
}
