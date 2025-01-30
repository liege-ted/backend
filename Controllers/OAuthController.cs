using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Discord.Rest;
using Discord;
using System.Net;
using TED.API.Models;
using TED.API.Extensions;
using TED.Models;

namespace TED.API.Controllers
{
    [ApiController]
    [Route("oauth")]
    public class OAuthController(IConfiguration configuration, IHttpClientFactory httpClientFactory) : BaseController
    {
        private readonly string _clientId = Environment.GetEnvironmentVariable("CLIENT_ID")!;
        private readonly string _clientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET")!;

        private readonly IConfiguration _configuration = configuration;

        [HttpGet]
        public async Task<IActionResult> HttpGetOAuthCodeAsync(string code)
        {
            if (string.IsNullOrEmpty(code))
                return JsonBadRequest("No code provided.");

            var client = httpClientFactory.CreateClient();

            Dictionary<string, string> keyValues = new()
            {
                { "client_id", _clientId },
                { "client_secret", _clientSecret },
                { "grant_type", "authorization_code" },
                { "code", code },
                { "redirect_uri", Environment.GetEnvironmentVariable("OAUTH_REDIRECT_URL") ?? "http://localhost:3001/oauth" }
            };             

            var content = new FormUrlEncodedContent(keyValues);

            // post to discord
            const string postUrl = "https://discord.com/api/v10/oauth2/token";
            var postResponse = await client.PostAsync(postUrl, content);

            if (postResponse.StatusCode != HttpStatusCode.OK) 
                return JsonBadRequest("Failed to get access token.");

            var responseContent = await postResponse.Content.ReadAsStringAsync();
            var response = JsonConvert.DeserializeObject<OAuthResponse>(responseContent);

            if (response is null) return JsonBadRequest("Failed to parse data!");

            // get data from discord
            var discordClient = new DiscordRestClient();
            await discordClient.LoginAsync(TokenType.Bearer, response.AccessToken);

            var user = await GetCreateWebSession.GetOrCreateWebSessionAsync(discordClient, response);

            var options = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None
            };

            // add jwt to cookies
            if (user.UserSecret != null) Response.Cookies.Append("token", user.UserSecret, options);

            return Redirect(Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "http://localhost:3000");
        }

        [HttpGet("user")]
        public async Task<IActionResult> HttpOAuthUserAsync()
        {
            try
            {
                // get session from cookies
                var session = await GetCreateWebSession.TryGetSessionFromRequestAsync(Request);
                if (session is null) return JsonNotFound();
                
                //get userinfo from discord
                var discordClient = new DiscordRestClient();
                await discordClient.LoginAsync(TokenType.Bearer, session.AccessToken);
                
                var user = new UserObject
                {
                    UserId = discordClient.CurrentUser.Id.ToString(),
                    Username = discordClient.CurrentUser.Username,
                    AvatarUrl = discordClient.CurrentUser.GetAvatarUrl()
                };  

                return JsonOk(user);
            }
            catch { return JsonInternalServerError(); }
        }

        [HttpGet("logout")]
        public IActionResult HttpOAuthLogoutAsync()
        {
            var options = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None
            };

            Response.Cookies.Delete("token", options);

            return Redirect(Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "http://localhost:3000");
        }
    }
}
