using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace TED.API.Models
{
    internal abstract class Jwt
    {
        private static readonly string Token = Environment.GetEnvironmentVariable("JWT_SECRET")!;

        /// <summary>
        /// Generates a JWT token with the given user id and access token
        /// </summary>
        /// <returns>A new JWT token</returns>
        public static string GenerateToken(string userId, string accessToken)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(Token);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[] { new Claim("user_id", userId), new Claim("token_access", accessToken) }),
                Expires = DateTime.UtcNow.AddYears(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
