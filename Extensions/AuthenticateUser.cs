using Discord.Rest;

namespace TED.API.Extensions
{
    public class AuthenticateUser
    {
        /// <summary>
        /// This method was replaced by the RequireUserPermissions attribute; however it is still functional
        /// and may be used for verifying if the provided user has permissions on the provided guild.
        /// </summary>
        [Obsolete("This method is deprecated, please use the RequireUserPermissions attribute instead.")]
        public static async Task<bool> UserHasGuildPermissionsAsync(DiscordRestClient botClient, HttpRequest request, ulong guildid, ulong userid)
        {
            var cookies = request.Cookies;
            if (!cookies.TryGetValue("token", out var jwt))
                return false;

            var user = await GetApiSession.TryGetUserFromApiKeyAsync(userid.ToString());

            if (user == null)
                return false;

            var restGuild = await botClient.GetGuildAsync(guildid);
            var guildUser = await restGuild.GetUserAsync(userid);
            
            return guildUser.GuildPermissions.ManageGuild;
        }
    }
}
