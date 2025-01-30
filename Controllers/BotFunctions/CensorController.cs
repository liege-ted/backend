using Discord.Rest;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using System.Net.Http.Headers;
using TED.API.Models;
using Newtonsoft.Json;
using TED.Services;
using TED.Models;
using MongoDB.Driver;
using TED.API.Extensions;
using TED.Additions.Enums;
using TED.API.Attributes;

namespace TED.API.Controllers.BotFunctions
{
    [ApiController]
    [Route("guilds/{id}/censors")]
    public class CensorController : BaseController
    {
        private string GuildId => RouteData.Values["id"]?.ToString() ?? string.Empty;
        private readonly DiscordRestClient _discordClient;
        private readonly IDatabase _redis;

        public CensorController(HttpClient httpClient, DiscordRestClient discordClient, IConnectionMultiplexer muxer)
        {
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("tedBackendCache", "1.0"));
            _redis = muxer.GetDatabase();
            _discordClient = discordClient;
        }

        /// <summary>
        /// Attempts to get censors from cache, if not found, fetches from database and caches it
        /// </summary>
        /// <returns>A json string of an array of Censor objects</returns>
        public async Task<string> GetCensorsAsync(string guildid)
        {
            var key = $"censors:{guildid}";
            string? json = await _redis.StringGetAsync(key);
            if (!string.IsNullOrEmpty(json)) return json;
            
            var censors = await MongoService.Censors.FindAsync(x => x.GuildId == guildid);
            var censorsArray = await censors.ToListAsync();
            json = JsonConvert.SerializeObject(censorsArray);
            await _redis.StringSetAsync(key, json, TimeSpan.FromDays(7));
            return json;
        }

        [HttpGet]
        [RequireGuildAuthentication]
        public async Task<IActionResult> HttpGetCensorsAsync()
        {
            try
            {
                var censorsJson = await GetCensorsAsync(GuildId);
                var censors = JsonConvert.DeserializeObject<Censor[]>(censorsJson);
                return JsonOk(censors);
            }
            catch { return JsonInternalServerError("Something went wrong!"); }
        }

        [HttpPost]
        [RequireGuildAuthentication]
        [RequiredBodyParam("terms")]
        [RequiredBodyParam("action")]
        public async Task<IActionResult> HttpInsertCensorsAsync()
        {
            try
            {
                // get body and deserialize
                var jsonBody = await new StreamReader(Request.Body).ReadToEndAsync();
                var obj = JsonConvert.DeserializeObject<BulkCensor>(jsonBody);

                if (obj?.Terms is null) return JsonBadRequest("Failed to parse data or data were invalid!");

                // param validation
                // term cap of 25 and char count of 512
                if (obj.Terms.Length > 25 || obj.Terms.Any(x => x.Length > 512))
                    return JsonBadRequest("You are either trying to add more than 25 terms at once or a term is longer than 512 characters! Both are disallowed.");
                // action has to be int within AutoAction
                if (!Enum.IsDefined(typeof(AutoAction), obj.Action!.Value))
                    return JsonBadRequest("The 'action' parameter should be a value >= 0 and <= 7!");
                
                // create censors
                var censors = obj.Terms.Select(x => new Censor
                {
                    GuildId = GuildId,
                    Term = x,
                    Action = obj.Action.Value // wont be null bc required in body attribute
                }).ToArray();

                // get current
                var currentCensorsJson = await GetCensorsAsync(GuildId);
                var currentCensors = JsonConvert.DeserializeObject<Censor[]>(currentCensorsJson);

                if (currentCensors is null) return JsonBadRequest("Failed to parse data or data were invalid!");
                
                // check for existing
                // if (currentCensors.Any(x => censors.Any(y => y.Term == x.Term && y.GuildId.ToString() == obj.GuildId)))
                //    return JsonBadRequest("Term already exists!");

                // insert into database
                await MongoService.Censors.InsertManyAsync(censors);

                // update cache
                var updated = currentCensors.Concat(censors).ToArray().Distinct();
                await _redis.UpdateCacheAsync($"censors:{GuildId}", JsonConvert.SerializeObject(updated));

                return JsonOk();
            }
            catch { return JsonInternalServerError("Something went wrong!"); }
        }

        [HttpDelete]
        [RequireGuildAuthentication]
        [RequiredBodyParam("terms")]
        public async Task<IActionResult> HttpDeleteCensorsAsync()
        {
            try
            {
                // get body and deserialize
                var jsonBody = await new StreamReader(Request.Body).ReadToEndAsync();
                var obj = JsonConvert.DeserializeObject<BulkCensor>(jsonBody);

                if (obj is null || obj.Terms is null)
                    return JsonBadRequest("Failed to parse data or data were invalid!");

                // create censors to be deleted
                var censors = obj.Terms.Select(x => new Censor
                {
                    GuildId = GuildId,
                    Term = x,
                });

                // delete from database
                var ls = censors.ToList();
                foreach (var censor in ls.ToList())
                {
                    // using deleteoneasync to avoid exceptions
                    try { await MongoService.Censors.DeleteOneAsync(x => x.GuildId == censor.GuildId && x.Term == censor.Term); }
                    catch { ls.Remove(censor); }
                }

                // update cache
                var currentCensorsJson = await GetCensorsAsync(GuildId);
                var currentCensors = JsonConvert.DeserializeObject<Censor[]>(currentCensorsJson);

                if (currentCensors is null)
                    return JsonBadRequest("Failed to parse data or data were invalid!");
                
                var updated = currentCensors.Except(ls, new CensorComparer()).ToArray();
                await _redis.UpdateCacheAsync($"censors:{GuildId}", JsonConvert.SerializeObject(updated));

                return JsonOk();
            }
            catch { return JsonInternalServerError("Something went wrong!"); }
        }
    }
}
