using Discord.Rest;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Newtonsoft.Json;
using StackExchange.Redis;
using System.Net.Http.Headers;
using TED.API.Models;
using TED.Services;

namespace TED.API.Controllers
{
    [ApiController]
    [Route("bot")]
    public class BotController : BaseController
    {
        private readonly DiscordRestClient _discordClient;
        private readonly IDatabase _redis;

        public BotController(HttpClient httpClient, DiscordRestClient discordClient, IConnectionMultiplexer muxer)
        {
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("tedBackendCache", "1.0"));
            _redis = muxer.GetDatabase();
            _discordClient = discordClient;
        }

        [HttpGet("stats")]
        public async Task<IActionResult> HttpGetBotStatsAsync()
        {
            string? json = await _redis.StringGetAsync("bot:stats");
            if (string.IsNullOrEmpty(json))
            {
                var guilds = await _discordClient.GetGuildsAsync(withCounts: true);
                var guildCount = guilds.Count;
                var userCount = guilds.Sum(guild => guild.ApproximateMemberCount ?? 0);
                var actionCount = MongoService.Logs.AsQueryable().Count();

                var stats = new Stats
                {
                    GuildCount = guildCount,
                    UserCount = userCount,
                    ActionCount = actionCount
                };

                await _redis.StringSetAsync("bot:stats", JsonConvert.SerializeObject(stats), TimeSpan.FromDays(1));
                json = JsonConvert.SerializeObject(stats);
            }
            
            var obj = JsonConvert.DeserializeObject<Stats>(json);
            return JsonOk(obj);
        }
    }
}
