using System.Net;
using Discord.Rest;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Newtonsoft.Json;
using StackExchange.Redis;
using System.Net.Http.Headers;
using Discord;
using TED.Additions;
using TED.API.Extensions;
using TED.API.Models;
using TED.Services;
using TED.Models;
using TED.Additions.Enums;
using TED.API.Attributes;

namespace TED.API.Controllers.BotFunctions
{
    [ApiController]
    [Route("guilds/{id}")]
    public class LogsController : BaseController
    {
        private string GuildId => RouteData.Values["id"]?.ToString() ?? string.Empty;
        private readonly DiscordRestClient _discordClient;
        private readonly IDatabase _redis;

        public LogsController(HttpClient httpClient, DiscordRestClient discordClient, IConnectionMultiplexer muxer)
        {
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("tedBackendCache", "1.0"));
            _redis = muxer.GetDatabase();
            _discordClient = discordClient;
        }

        /// <summary>
        /// Attempts to get logs from the cache, if not found, it will fetch it from database and cache it
        /// </summary>
        /// <returns>A json string of an array of Log objects</returns>
        public async Task<string> GetLogsAsync(string guildid)
        {
            var key = $"logs:{guildid}";
            string? json = await _redis.StringGetAsync(key);
            if (!string.IsNullOrEmpty(json)) return json;
            
            // get logs from last 7 days
            var a = MongoService.Logs.AsQueryable();
            var logs = await MongoService.Logs.Find(x => x.GuildId == guildid && DateTimeOffset.Now.ToUnixTimeSeconds() - x.DateTime <= 604800).ToListAsync();
            json = JsonConvert.SerializeObject(logs);
            await _redis.StringSetAsync(key, json, TimeSpan.FromDays(1));
            return json;
        }

        /// <summary>
        /// Unique to the LogsController to store a DiscordUser object associated with a log
        /// </summary>
        private class DiscordLog
        {
            [JsonProperty("log")]
            public Log? Log { get; set; }
            [JsonProperty("user")]
            public DiscordUser? User { get; set; }
            [JsonProperty("moderator")]
            public DiscordUser? ModeratorUser { get; set; }
        }

        /// <summary>
        /// Attempts to get a Discord user from the cache, if not found, will fetch from the Discord API and cache it
        /// </summary>
        /// <returns>A DiscordUser object associated with the provided user id; returns bots automod if 0 or null</returns>
        public async Task<DiscordUser> GetDiscordUserAsync(string id)
        {
            string? json = await _redis.StringGetAsync($"discordUser:{id}");
            if (string.IsNullOrEmpty(json))
            {
                var restUser = await _discordClient.GetUserAsync(ulong.Parse(id));
                var discordUser = new DiscordUser()
                {
                    Id = restUser.Id.ToString(),
                    Username = restUser.Username,
                    AvatarUrl = restUser.GetAvatarUrl()
                };
                json = JsonConvert.SerializeObject(discordUser);
                await _redis.StringSetAsync($"discordUser:{id}", json, TimeSpan.FromDays(3));
            }
            
            var obj = JsonConvert.DeserializeObject<DiscordUser>(json);
            if (obj is null || id == "0") return new DiscordUser()
            {
                Id = "0",
                Username = "TED Automod",
                AvatarUrl = "https://cdn.discordapp.com/avatars/879360985738117120/c64018b70d892b290dc9327a1a41ea67.webp?size=160"
            };
            return obj;
        }

        [HttpGet("logs")]
        [RequireGuildAuthentication]
        public async Task<IActionResult> HttpGetLogsAsync()
        {
            var logs = await GetLogsAsync(GuildId);
            var logsObject = JsonConvert.DeserializeObject<Log[]>(logs);

            if (logsObject is null) return JsonNotFound();
            
            var discordLogs = new List<DiscordLog>();

            foreach (var log in logsObject)
            {
                var discordLog = new DiscordLog()
                {
                    Log = log,
                    User = await GetDiscordUserAsync(log.UserId),
                    ModeratorUser = await GetDiscordUserAsync(log.ModeratorId)
                };
                discordLogs.Add(discordLog);
            }

            return JsonOk(discordLogs);
        }

        [HttpGet("logs/{userid}")]
        [RequireGuildAuthentication]
        public async Task<IActionResult> HttpGetUserLogsAsync([FromRoute]string userid)
        {
            var logs = await GetLogsAsync(GuildId);
            var logsObject = JsonConvert.DeserializeObject<Log[]>(logs);

            if (logsObject is null) return JsonNotFound();
            
            var discordLogs = new List<DiscordLog>();

            var userlogs = logsObject.Where(x => x.UserId == userid).ToList();
            if (userlogs.Count == 0) return JsonNotFound();
            
            foreach (var log in userlogs)
            {
                var discordLog = new DiscordLog()
                {
                    Log = log,
                    User = await GetDiscordUserAsync(userid),
                    ModeratorUser = await GetDiscordUserAsync(log.ModeratorId)
                };
                discordLogs.Add(discordLog);
            }

            return JsonOk(discordLogs);
        }

        [HttpPost("users/{userid}/logs")]
        [RequireGuildAuthentication]
        [RequiredBodyParam("type")]
        [RequiredBodyParam("reason")]
        public async Task<IActionResult> HttpPostLogAsync([FromRoute]string userid)
        {
            try 
            { 
                var jsonBody = await new StreamReader(Request.Body).ReadToEndAsync();
                var logObject = JsonConvert.DeserializeObject<Log>(jsonBody);

                if (logObject is null) return JsonBadRequest("Failed to parse data or data were invalid!");
                
                // only allow 'manual' logs, not automatic
                if (!((int)logObject.Type >= 6 && (int)logObject.Type <= 9))
                    return JsonBadRequest("Parameter 'type' must be >= 6 and <= 9!");

                switch (logObject.Type)
                {
                    // require ban days when type is ban
                    case InfractType.ManualBan when logObject.BanDays == 0:
                        return JsonBadRequest("Parameter 'banDays' must be >= 1 when 'type' is 9!");
                    // require timeout time when type is timeout
                    case InfractType.ManualTimeout when logObject.TimeoutTime == 0:
                        return JsonBadRequest("Parameter 'timeoutTime' must be >= 1 when 'type' is 7!");
                }

                var userObject = await GetCreateWebSession.TryGetSessionFromRequestAsync(Request);
                if (userObject == null) return JsonNotFound();
                
                // check if user is bot or logging themselves
                if (userObject.UserId == "0") return JsonBadRequest("Cannot log the bot!");
                if (userObject.UserId == userid) return JsonBadRequest("Cannot log yourself!");

                // fill in missing values
                await logObject.ModifyAsync(x =>
                {
                    x.DateTime = DateTime.UtcNow.ToUnix();
                    x.ModeratorId = userObject.UserId;
                    x.CaseId = GeneralAdditions.RandomInt(5);
                });

                IGuild guild;
                try { guild = await _discordClient.GetGuildAsync(ulong.Parse(logObject.GuildId)); }
                catch { return JsonInternalServerError("Could not get guild!"); }

                IGuildUser user;
                try { user = await guild.GetUserAsync(ulong.Parse(logObject.UserId)); }
                catch { return JsonInternalServerError("Could not get user!"); }
                
                // notify user in dms?
                var notified = false;
                if (logObject.NotifyUser)
                    notified = await LogMethods.NotifyUserAsync(guild, user, logObject);
                
                // set status of user notif
                object? notifyStatus = logObject.NotifyUser switch
                {
                    true when !notified => new { code = 500, message = "Failed to notify user!" },
                    true when notified => new { code = 200, message = "Notified user in DMs!" },
                    _ => null
                };

                // log to channel?
                var logged = false;
                if (logObject.LogToChannel)
                    logged = await LogMethods.ModlogAsync(guild, user, logObject, notified);
                
                // set status of log to channel
                object? logChannelStatus = logObject.LogToChannel switch
                {
                    true when !logged => new { code = 500, message = "Failed to log to modlog channel!" },
                    true when logged => new { code = 200, message = "Logged to modlog channel!" },
                    _ => null
                };
                
                // set status of action taken against user
                object? actionStatus = null;
                if (logObject.TakeAction)
                {
                    switch (logObject.Type)
                    {
                        case InfractType.AutoWarning:
                            actionStatus = new { code = 200, message = "Warned user!" };
                            break;
                        case InfractType.ManualTimeout:
                            try
                            {
                                var timeoutSeconds = logObject.TimeoutTime;
                                await user.SetTimeOutAsync(new TimeSpan(0, 0, (int)timeoutSeconds),
                                    new RequestOptions() { AuditLogReason = logObject.Reason });
                                actionStatus = new { code = 200, message = "Timed out user!" };
                            }
                            catch { actionStatus = new { code = 500, message = "Failed to timeout user!" }; }
                            break;
                        case InfractType.ManualKick:
                            try
                            {
                                await user.KickAsync(logObject.Reason, new RequestOptions() { AuditLogReason = logObject.Reason });
                                actionStatus = new { code = 200, message = "Kicked user!" };
                            }
                            catch { actionStatus = new { code = 500, message = "Failed to kick user!" }; }
                            break;
                        case InfractType.ManualBan:
                            try
                            {
                                await user.BanAsync(logObject.BanDeleteDays, logObject.Reason, new RequestOptions() { AuditLogReason = logObject.Reason });
                                actionStatus = new { code = 200, message = $"Banned user for {logObject.BanDeleteDays} days and deleted {logObject.BanDeleteDays} days of messages!" };
                            }
                            catch { actionStatus = new { code = 500, message = "Failed to ban user!" }; }
                            break;
                    }
                }
                
                Response.ContentType = "application/json";
                return StatusCode(207, new
                {
                    logStatus = new { code = 200, message = "Log created!" },
                    notifyStatus,
                    logChannelStatus,
                    actionStatus
                });
            }
            catch { return JsonInternalServerError("Something went wrong!"); }
        }
    }
}
