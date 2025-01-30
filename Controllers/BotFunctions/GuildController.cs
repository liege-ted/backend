using Discord;
using Discord.Rest;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using StackExchange.Redis;
using System.Net;
using System.Net.Http.Headers;
using TED.Additions;
using TED.API.Attributes;
using TED.API.Extensions;
using TED.API.Models;

namespace TED.API.Controllers.BotFunctions
{
    [ApiController]
    [Route("guilds/{id}")]
    public class GuildController : BaseController
    {
        private string GuildId => RouteData.Values["id"]?.ToString() ?? string.Empty;
        private readonly DiscordRestClient _discordClient;
        private readonly IDatabase _redis;

        public GuildController(HttpClient httpClient, DiscordRestClient discordClient, IConnectionMultiplexer muxer)
        {
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("tedBackendCache", "1.0"));
            _redis = muxer.GetDatabase();
            _discordClient = discordClient;
        }

        /// <summary>
        /// Attempts to get a guild from cache, if not found, fetches from Discord API and caches it
        /// </summary>
        /// <param name="user">The WebSession object containing the users access token, id, secret, etc</param>
        /// <returns>A json string of the Guild object</returns>
        public async Task<string> GetGuildAsync(string guildid, WebSession user)
        {
            var key = $"guild:{guildid}";
            string? json = await _redis.StringGetAsync(key);
            if (!string.IsNullOrEmpty(json)) return json;
            var restClient = new DiscordRestClient();
            await restClient.LoginAsync(TokenType.Bearer, user.AccessToken);

            var restGuilds = await restClient.GetGuildSummariesAsync().FlattenAsync();
            if (restGuilds == null) return HttpStatusCode.NotFound.ToString();

            var restGuild = restGuilds.FirstOrDefault(g => g.Id.ToString() == guildid);
            if (restGuild == null) return HttpStatusCode.NotFound.ToString();

            var inGuild = await _discordClient.IsInGuild(guildid);
            var owner = await _discordClient.GetGuildAsync(restGuild.Id).Result.GetOwnerAsync();

            var guildObject = new Guild()
            {
                Id = restGuild.Id.ToString(),
                Name = restGuild.Name,
                Icon = restGuild.IconUrl,
                Permissions = restGuild.Permissions.ToString(),
                InGuild = inGuild,
                MemberCount = restGuild.ApproximateMemberCount ?? -1,
                Owner = owner.Username
            };

            json = JsonConvert.SerializeObject(guildObject);
            await _redis.StringSetAsync(key, json, TimeSpan.FromDays(7));

            return json;
        }

        [HttpGet("guild")]
        public async Task<IActionResult> HttpGetGuildAsync()
        {
            var user = await GetCreateWebSession.TryGetSessionFromRequestAsync(Request);
            if (user == null) return JsonNotFound();

            var guild = await GetGuildAsync(GuildId, user);
            if (guild == HttpStatusCode.NotFound.ToString()) return JsonNotFound();

            var guildObject = JsonConvert.DeserializeObject<Guild>(guild);

            return JsonOk(guildObject);
        }

        /// <summary>
        /// Attempts to get channels from cache, if not found, fetches from Discord API and caches it
        /// </summary>
        /// <returns>A json string of an array of Channel objects</returns>
        public async Task<string> GetChannelsAsync(string guildid)
        {
            var key = $"channels:{guildid}";
            string? json = await _redis.StringGetAsync(key);
            if (!string.IsNullOrEmpty(json)) return json;
            var restGuild = await _discordClient.GetGuildAsync(ulong.Parse(guildid));
            if (restGuild == null) return HttpStatusCode.NotFound.ToString();

            var restChannels = await restGuild.GetChannelsAsync();

            var channels = restChannels.Select(c => new Channel()
            {
                Id = c.Id.ToString(),
                Name = c.Name,
                Position = c.Position,
            });

            json = JsonConvert.SerializeObject(channels);
            await _redis.StringSetAsync(key, json, TimeSpan.FromDays(7));

            return json;
        }

        [HttpGet("channels")]
        public async Task<IActionResult> HttpGetChannelsAsync()
        {
            var user = await GetCreateWebSession.TryGetSessionFromRequestAsync(Request);
            if (user == null) return JsonNotFound();

            var channels = await GetChannelsAsync(GuildId);
            if (channels == HttpStatusCode.NotFound.ToString()) return JsonNotFound();

            var channelsObject = JsonConvert.DeserializeObject<Channel[]>(channels);

            return JsonOk(channelsObject);
        }

        public async Task<string> GetCategoriesAsync(string guildid)
        {
            var key = $"categories:{guildid}";
            string? json = await _redis.StringGetAsync(key);
            if (!string.IsNullOrEmpty(json)) return json;
            var restGuild = await _discordClient.GetGuildAsync(ulong.Parse(guildid));
            if (restGuild == null) return HttpStatusCode.NotFound.ToString();

            var restGuildChannels = await restGuild.GetChannelsAsync();
            var restCategories = restGuildChannels.Where(c => c.GetChannelType() == ChannelType.Category);
            var restChannels = restGuildChannels.Where(c => c.GetChannelType() == ChannelType.Text);

            var categories = restCategories.Select(cat => new Category()
            {
                Name = cat.Name,
                Channels = restChannels.Where(c => (c as RestTextChannel)?.CategoryId == cat.Id).Select(c => new Channel()
                {
                    Id = c.Id.ToString(),
                    Name = c.Name,
                    Position = c.Position,
                }).ToArray()
            });

            json = JsonConvert.SerializeObject(categories);
            await _redis.StringSetAsync(key, json, TimeSpan.FromDays(7));

            return json;
        }

        [HttpGet("categories")]
        public async Task<IActionResult> HttpGetCategoriesAsync()
        {
            var user = await GetCreateWebSession.TryGetSessionFromRequestAsync(Request);
            if (user == null) return JsonNotFound();

            var categories = await GetCategoriesAsync(GuildId);
            if (categories == HttpStatusCode.NotFound.ToString()) return JsonNotFound();

            var categoriesObject = JsonConvert.DeserializeObject<Category[]>(categories);

            return JsonOk(categoriesObject);
        }
    }
}
