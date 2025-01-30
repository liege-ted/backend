using Discord.Rest;
using TED.Additions;
using TED.API.Models;
using TED.Models;

namespace TED.API.Extensions
{
    public static class GuildAuth
    {
        /// <summary>
        /// Used to authenticate a user with a guild ensuring they have the required permissions
        /// </summary>
        public static async Task<bool> AuthenticateUserWithGuildAsync(this HttpRequest request, DiscordRestClient restClient, string guildid)
        {
            try
            {
                string? token;
                // get token from cookies or header
                if (request.Headers.TryGetValue("Authorization", out var auth))
                {
                    token = auth.ToString().Replace("Bearer ", "");
                }
                else if (!request.Cookies.TryGetValue("token", out token))
                {
                    return false;
                }

                // get session object from token
                var sessionObject = await GetCreateWebSession.TryGetSessionFromJwtAsync(token);
                var apikeyObject = await GetApiSession.TryGetUserFromApiKeyAsync(token);
                if (sessionObject is null && apikeyObject is null)
                    return false;

                // get user from discord
                RestUser user;
                if (sessionObject is not null)
                    user = await restClient.GetUserAsync(ulong.Parse(sessionObject.UserId));
                else
                    user = await restClient.GetUserAsync(ulong.Parse(apikeyObject.UserId));
                if (user is null)
                    return false;

                // get guild from discord
                var guild = await restClient.GetGuildAsync(ulong.Parse(guildid));
                if (guild is null)
                    return false;

                // get guild user from discord
                RestGuildUser guildUser;
                if (sessionObject is not null)
                    guildUser = await guild.GetUserAsync(ulong.Parse(sessionObject.UserId));
                else
                    guildUser = await guild.GetUserAsync(ulong.Parse(apikeyObject.UserId));

                int manageGuild = 32;

                // check if user has permission to manage guild
                var hasPermissionInGuild = (guildUser.GuildPermissions.RawValue & (ulong)manageGuild) != 0;
                return hasPermissionInGuild;
            }
            catch
            {
                return false;
            }
        }
    }
}
