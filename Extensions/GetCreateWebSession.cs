using Discord.Rest;
using MongoDB.Driver;
using TED.API.Models;
using TED.API.Services;
using TED.Models;

namespace TED.API.Extensions
{
    public static class GetCreateWebSession
    {
        /// <summary>
        /// Gets or creates a web session for a user using the web interface, not the public facing API
        /// </summary>
        /// <returns>Returns an existing or new WebSession object for the user using the web interface</returns>
        public static async Task<WebSession> GetOrCreateWebSessionAsync(DiscordRestClient discordClient, OAuthResponse response)
        {
            // attempt to find existing websession object for user client
            var user = await MongoService.WebSessions.Find(x => x.UserId == discordClient.CurrentUser.Id.ToString()).FirstOrDefaultAsync();
            
            // if user object does not exist, create a new one
            if (user == null)
            {
                // generate jwt token for user
                var jwt = Jwt.GenerateToken(discordClient.CurrentUser.Id.ToString(), response.AccessToken);
                user = new WebSession()
                {
                    UserId = discordClient.CurrentUser.Id.ToString(),
                    AccessToken = response.AccessToken,
                    RefreshToken = response.RefreshToken,
                    ExpireDate = DateTime.Now.AddSeconds(response.ExpiresIn),
                    UserSecret = jwt,
                };
            }
            // update discord access token if it has changed
            else if (user.AccessToken != response.AccessToken)
            {
                var jwt = Jwt.GenerateToken(discordClient.CurrentUser.Id.ToString(), response.AccessToken);
                user.AccessToken = response.AccessToken;
                user.RefreshToken = response.RefreshToken;
                user.ExpireDate = DateTime.Now.AddSeconds(response.ExpiresIn);
                user.UserSecret = jwt;
            }
            
            await user.SaveAsync();
            return user;
        }
        
        /// <summary>
        /// Attempts to get a WebSession object from the provided JWT token; simple helper function
        /// </summary>
        /// <returns>The WebSession object associated with the provided JWT</returns>
        public static async Task<WebSession?> TryGetSessionFromJwtAsync(string jwt)
            => await MongoService.WebSessions.Find(x => x.UserSecret == jwt).FirstOrDefaultAsync();
        
        /// <summary>
        /// Attempts to get a WebSession object from the provided HttpRequest object; another simple helper function
        /// </summary>
        /// <returns>The WebSession object associated with the provided HttpRequest</returns>
        public static async Task<WebSession?> TryGetSessionFromRequestAsync(HttpRequest request)
        {
            string? jwt;
            // get jwt from cookies or header
            if (request.Headers.TryGetValue("Authorization", out var auth))
                jwt = auth.ToString();
            else if (!request.Cookies.TryGetValue("token", out jwt))
                return null;
            
            // get user object from jwt
            var userObject = await TryGetSessionFromJwtAsync(jwt);
            return userObject ?? null;
        }
    }
}
