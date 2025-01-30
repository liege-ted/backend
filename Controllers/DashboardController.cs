using Discord;
using Discord.Rest;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using StackExchange.Redis;
using System.Net.Http.Headers;
using TED.API.Extensions;
using TED.API.Models;

namespace TED.API.Controllers
{
    [ApiController]
    [Route("")]
    public class DashboardController : BaseController
    {
        private const ulong BotId = 879360985738117120;
        private readonly DiscordRestClient _discordClient;
        private readonly IDatabase _redis;

        public DashboardController(HttpClient httpClient, DiscordRestClient discordClient, IConnectionMultiplexer muxer)
        {
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("tedBackendCache", "1.0"));
            _redis = muxer.GetDatabase();
            _discordClient = discordClient;
        }
        
        /// <summary>
        /// Attempts to get the guilds the user has permission to manage from cache, otherwise fetches from the Discord API, then caches
        /// </summary>
        /// <param name="user"></param>
        /// <returns>A json string of an array of Guild objects</returns>
        public async Task<string> GetDashboardGuildsAsync(WebSession user)
        {
            // check cache
            var key = $"dashboard:guilds:{user.UserId}";
            string? json = await _redis.StringGetAsync(key);
            if (!string.IsNullOrEmpty(json)) return json;
            
            // login to discord
            var discordClient = new DiscordRestClient();
            await discordClient.LoginAsync(TokenType.Bearer, user.AccessToken);

            // get guilds and filter by manage guild permission
            var guilds = await discordClient.GetGuildSummariesAsync().FlattenAsync();
            var permissionGuilds = await discordClient.GetPermissionGuildsAsync(guilds.ToArray());

            // cache and return
            json = JsonConvert.SerializeObject(permissionGuilds);
            await _redis.StringSetAsync(key, json, TimeSpan.FromDays(7));
            return json;
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> HttpGetDashboardGuildsAsync()
        {
            var user = await GetCreateWebSession.TryGetSessionFromRequestAsync(Request);
            if (user == null) return JsonNotFound();

            var guilds = await GetDashboardGuildsAsync(user);
            return JsonOk(guilds);
        }
    }
}
