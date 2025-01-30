using Discord.Rest;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using StackExchange.Redis;
using System.Net;
using System.Net.Http.Headers;
using TED.Additions;
using TED.API.Attributes;
using TED.API.Extensions;
using TED.Models;

namespace TED.API.Controllers.BotFunctions
{
    [ApiController]
    [Route("guilds/{id}/settings")]
    public class SettingsController : BaseController
    {
        private string GuildId => RouteData.Values["id"]?.ToString() ?? string.Empty;
        private readonly DiscordRestClient _discordClient;
        private readonly IDatabase _redis;
        
        public SettingsController(HttpClient httpClient, DiscordRestClient discordClient, IConnectionMultiplexer muxer)
        {
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("tedBackendCache", "1.0"));
            _redis = muxer.GetDatabase();
            _discordClient = discordClient;
        }

        /// <summary>
        /// Attempts to get guild settings from cache, if not found, will get from database and cache it
        /// </summary>
        /// <returns>A json string of a GuildSetttings object</returns>
        public async Task<string> GetAllGuildSettingsAsync(string guildid)
        {
            var key = $"guildSettings:{guildid}";
            string? json = await _redis.StringGetAsync(key);
            if (!string.IsNullOrEmpty(json)) return json;
            
            var guildObject = await GuildSettingsExtensions.GetGuildObjectAsync(guildid);
            if (guildObject == null) return HttpStatusCode.NotFound.ToString();
            json = JsonConvert.SerializeObject(guildObject);
            await _redis.StringSetAsync(key, json, TimeSpan.FromDays(7));

            return json;
        }
        
        [HttpPut]
        [RequireGuildAuthentication]
        public async Task<IActionResult> HttpPutGuildSettingsAsync()
        {
            // try
            // {
            //     
            // }
            // catch { return JsonInternalServerError("Something went wrong!"); }
            
            // parse json into GuildSettings object
                var json = await new StreamReader(Request.Body).ReadToEndAsync();
                GuildSettings newSettings;
                try
                {
                    newSettings = JsonConvert.DeserializeObject<GuildSettings>(json);
                    if (newSettings is null)
                        return JsonBadRequest("Failed to parse data or data were invalid!");
                }
                catch
                {
                    return JsonBadRequest("Failed to parse data or data were invalid!");
                }

                // get current settings from db
                var oldSettings = await GuildSettingsExtensions.GetGuildObjectAsync(GuildId);
                if (oldSettings is null) return JsonBadRequest("Could not get guild object!");

                var updatedSettings = oldSettings;

                // config settings
                foreach (var newSetting in newSettings.Settings)
                {
                    var type = newSetting.Setting;
                    
                    // validate
                    if ((int)newSetting.Action < 0 || (int)newSetting.Action > 7)
                        return JsonBadRequest("Action must be between 0 and 7!");
                    
                    var i = updatedSettings.Settings.ToList().FindIndex(x => x.Setting == type);
                    updatedSettings.Settings[i] = newSetting;
                }
                
                // channel settings
                if (!string.IsNullOrEmpty(newSettings.Channels.ModlogChannelId))
                    updatedSettings.Channels.ModlogChannelId = newSettings.Channels.ModlogChannelId;
                if (!string.IsNullOrEmpty(newSettings.Channels.EventLogChannelId))
                    updatedSettings.Channels.EventLogChannelId = newSettings.Channels.EventLogChannelId;

                // update in db
                await oldSettings.ModifyAsync(x => x = updatedSettings);
                
                // update cache
                var key = $"guildSettings:{GuildId}";
                var jsonSettings = JsonConvert.SerializeObject(updatedSettings);
                await _redis.StringSetAsync(key, jsonSettings, TimeSpan.FromDays(7));
                
                return JsonOk(updatedSettings);
        }
        
        [HttpGet]
        [RequireGuildAuthentication]
        public async Task<IActionResult> HttpGetAllGuildSettingsAsync()
        {
            var guildObject = await GetAllGuildSettingsAsync(GuildId);
            if (guildObject == HttpStatusCode.NotFound.ToString()) return JsonNotFound();

            return JsonOk(guildObject);
        }
    }
}
