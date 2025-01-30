using Discord.Rest;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using System.Net.Http.Headers;
using TED.API.Models;
using Newtonsoft.Json;
using MongoDB.Driver;
using TED.API.Attributes;
using TED.API.Extensions;
using TED.Services;
using TED.Models;

namespace TED.API.Controllers.BotFunctions
{
    [ApiController]
    [Route("guilds/{id}/regex")]
    public class RegexController : BaseController
    {
        private string GuildId => RouteData.Values["id"]?.ToString() ?? string.Empty;
        private readonly DiscordRestClient _discordClient;
        private readonly IDatabase _redis;

        public RegexController(HttpClient httpClient, DiscordRestClient discordClient, IConnectionMultiplexer muxer)
        {
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("tedBackendCache", "1.0"));
            _redis = muxer.GetDatabase();
            _discordClient = discordClient;
        }

        /// <summary>
        /// Attempts to get guild regexs from cache, if not found, will get from database and put in cache
        /// </summary>
        /// <returns>A json string of an array containing Regex objects</returns>
        public async Task<string> GetRegexsAsync(string guildid)
        {
            var key = $"regexs:{guildid}";
            string? json = await _redis.StringGetAsync(key);
            if (!string.IsNullOrEmpty(json)) return json;
            
            // cache IS empty, get from database and insert into cache
            var regexs = await MongoService.RegexStatements.FindAsync(x => x.GuildId == guildid);
            var regexsArray = await regexs.ToListAsync();
            json = JsonConvert.SerializeObject(regexsArray);
            await _redis.StringSetAsync(key, json, TimeSpan.FromDays(7));
            return json;
        }

        [HttpGet]
        [RequireGuildAuthentication]
        public async Task<IActionResult> HttpGetRegexsAsync()
        {
            try
            {
                var regexsJson = await GetRegexsAsync(GuildId);
                var regexs = JsonConvert.DeserializeObject<RegexStatement[]>(regexsJson);
                return JsonOk(regexs);
            }
            catch { return JsonInternalServerError("Something went wrong!"); }
        }

        [HttpPost]
        [RequireGuildAuthentication]
        [RequiredBodyParam("statement")]
        [RequiredBodyParam("action")]
        public async Task<IActionResult> HttpInsertRegexAsync()
        {
            try
            {
                // get body and deserialize
                var jsonBody = await new StreamReader(Request.Body).ReadToEndAsync();
                var obj = JsonConvert.DeserializeObject<RegexStatement>(jsonBody);

                if (obj is null) return JsonBadRequest("Failed to parse data or data were invalid!");

                // param validation
                // statement cannot be more than 4096 chars
                if (obj.Statement.Length > 4096)
                    return JsonBadRequest("Statement cannot be more than 4096 characters!");
                // action must be an AutoAction
                if ((int)obj.Action < 0 || (int)obj.Action > 7)
                    return JsonBadRequest("The 'action' parameter should be a value >= 0 and <= 7!");
                
                // convert to Regex
                var regex = new RegexStatement
                {
                    GuildId = GuildId,
                    Statement = obj.Statement,
                    Action = obj.Action
                };

                // get current
                var currentRegexsJson = await GetRegexsAsync(GuildId);
                var currentRegexs = JsonConvert.DeserializeObject<RegexStatement[]>(currentRegexsJson);

                if (currentRegexs is null) return JsonBadRequest("Failed to parse data or data were invalid!");
                
                // check if already exists
                if (currentRegexs.Any(x => x.Statement == regex.Statement))
                    return JsonBadRequest("Regex already exists!");

                // insert into database
                await MongoService.RegexStatements.InsertOneAsync(regex);

                // update cache
                var updated = currentRegexs.Concat([regex]).ToArray().Distinct();
                await _redis.UpdateCacheAsync($"regexs:{GuildId}", JsonConvert.SerializeObject(updated));

                return JsonOk();
            }
            catch { return JsonInternalServerError("Something went wrong!"); }
        }

        [HttpDelete]
        [RequireGuildAuthentication]
        [RequiredBodyParam("statement")]
        public async Task<IActionResult> HttpDeleteRegexAsync()
        {
            try
            {
                // get body and deserialize
                var jsonBody = await new StreamReader(Request.Body).ReadToEndAsync();
                var obj = JsonConvert.DeserializeObject<RegexStatement>(jsonBody);

                if (obj is null) return JsonBadRequest("Failed to parse data or data were invalid!");

                // convert to Regex
                var regex = new RegexStatement
                {
                    GuildId = GuildId,
                    Statement = obj.Statement,
                    Action = obj.Action
                };

                // delete from database
                await MongoService.RegexStatements.DeleteOneAsync(x => x.Statement == regex.Statement && x.GuildId == regex.GuildId);

                // update cache
                var currentRegexsJson = await GetRegexsAsync(GuildId);
                var currentRegexs = JsonConvert.DeserializeObject<RegexStatement[]>(currentRegexsJson);

                if (currentRegexs is null) return JsonBadRequest("Failed to parse data or data were invalid!");
                
                var updated = currentRegexs.Except([regex], new RegexComparor()).ToArray();
                await _redis.UpdateCacheAsync($"regexs:{GuildId}", JsonConvert.SerializeObject(updated));

                return JsonOk();
            }
            catch { return JsonInternalServerError("Something went wrong!"); }
        }
    }
}
