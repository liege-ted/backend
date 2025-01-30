using Discord.Rest;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using System.Net.Http.Headers;
using TED.API.Models;
using Newtonsoft.Json;
using MongoDB.Driver;
using TED.API.Attributes;
using TED.API.Extensions;
using TED.Models;
using TED.Services;

namespace TED.API.Controllers.BotFunctions
{
    [ApiController]
    [Route("guilds/{id}/shortcuts")]
    public class ShortcutsController : BaseController
    {
        private string GuildId => RouteData.Values["id"]?.ToString() ?? string.Empty;
        private readonly IDatabase _redis;

        public ShortcutsController(HttpClient httpClient, IConnectionMultiplexer muxer)
        {
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("tedBackendCache", "1.0"));
            _redis = muxer.GetDatabase();
        }

        /// <summary>
        /// Attempts to get shortcuts from cache, if not found, fetches from database and caches it
        /// </summary>
        /// <returns>A json string of an array of ReasonShortcut objects</returns>
        public async Task<string> GetShortcutsAsync(string guildid)
        {
            var key = $"shortcuts:{guildid}";
            string? json = await _redis.StringGetAsync(key);
            if (!string.IsNullOrEmpty(json)) return json;
            
            var shortcuts = await MongoService.ReasonShortcuts.FindAsync(x => x.GuildId == guildid);
            var shortcutsArray = await shortcuts.ToListAsync();
            json = JsonConvert.SerializeObject(shortcutsArray);
            await _redis.StringSetAsync(key, json, TimeSpan.FromDays(7));
            return json;
        }

        [HttpGet]
        [RequireGuildAuthentication]
        public async Task<IActionResult> HttpGetShortcutsAsync()
        {
            try
            {
                var shortcutsJson = await GetShortcutsAsync(GuildId);
                var shortcuts = JsonConvert.DeserializeObject<ReasonShortcut[]>(shortcutsJson);
                return JsonOk(shortcuts);
            }
            catch { return JsonInternalServerError("Something went wrong!"); }
        }

        [HttpPost]
        [RequireGuildAuthentication]
        [RequiredBodyParam("shortcut")]
        [RequiredBodyParam("reason")]
        public async Task<IActionResult> HttpPostShortcutAsync()
        {
            try
            {
                // get body and deserialize
                var jsonBody = await new StreamReader(Request.Body).ReadToEndAsync();
                var obj = JsonConvert.DeserializeObject<ReasonShortcut>(jsonBody);
                
                if (obj is null) return JsonBadRequest("Failed to parse data!");

                // create shortcut
                var shortcut = new ReasonShortcut
                {
                    GuildId = obj.GuildId,
                    Shortcut = obj.Shortcut,
                    Reason = obj.Reason
                };

                // get current
                var currentShortcutsJson = await GetShortcutsAsync(obj.GuildId);
                var currentShortcuts = JsonConvert.DeserializeObject<ReasonShortcut[]>(currentShortcutsJson);

                if (currentShortcuts is null) return JsonBadRequest("Failed to parse data or data were invalid!");
                
                // check for existing
                if (currentShortcuts.Any(x => x.Shortcut == shortcut.Shortcut && x.GuildId.ToString() == obj.GuildId))
                    return JsonBadRequest("Shortcut already exists!");

                // insert into database
                await MongoService.ReasonShortcuts.InsertOneAsync(shortcut);

                // update cache
                var updated = currentShortcuts.Concat([shortcut]).ToArray().Distinct();
                await _redis.UpdateCacheAsync($"shortcuts:{obj.GuildId}", JsonConvert.SerializeObject(updated));

                return JsonOk();
            }
            catch { return JsonInternalServerError("Something went wrong!"); }
        }

        [HttpDelete]
        [RequireGuildAuthentication]
        [RequiredBodyParam("shortcut")]
        public async Task<IActionResult> HttpDeleteShortcutAsync()
        {
            try
            {
                // get body and deserialize
                var jsonBody = await new StreamReader(Request.Body).ReadToEndAsync();
                var obj = JsonConvert.DeserializeObject<ReasonShortcut>(jsonBody);

                if (obj is null) return JsonBadRequest("Failed to parse data!");

                // create shortcut to be deleted
                var shortcut = new ReasonShortcut
                {
                    GuildId = obj.GuildId,
                    Shortcut = obj.Shortcut,
                    Reason = obj.Reason
                };

                // delete from database
                await MongoService.ReasonShortcuts.DeleteOneAsync(x =>
                    x.GuildId == shortcut.GuildId && x.Shortcut == shortcut.Shortcut);
                
                // update cache
                var currentShortcutsJson = await GetShortcutsAsync(obj.GuildId);
                var currentShortcuts = JsonConvert.DeserializeObject<ReasonShortcut[]>(currentShortcutsJson);

                if (currentShortcuts is null) return JsonBadRequest("Failed to parse data or data were invalid!");
                
                var updated = currentShortcuts.Except([shortcut], new ReasonShortcutComparer()).ToArray();
                await _redis.UpdateCacheAsync($"shortcuts:{obj.GuildId}", JsonConvert.SerializeObject(updated));

                return JsonBadRequest();
                
                return JsonOk();
            }
            catch { return JsonInternalServerError("Something went wrong!"); }
        }
    }
}
