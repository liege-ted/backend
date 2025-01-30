using Discord.Rest;
using TED.API.Models;

namespace TED.API.Extensions
{
    public static class GuildExtensions
    {
        /// <summary>
        /// Gets the guilds the user has permission to manage
        /// </summary>
        /// <param name="guilds">An array of RestUserGuilds returned from the Discord API</param>
        /// <returns>An array of Guild objects</returns>
        public static async Task<Guild[]> GetPermissionGuildsAsync(this DiscordRestClient discordClient, RestUserGuild[] guilds)
        {
            List<Guild> permissionGuilds = [];
            const int manageGuild = 32;

            // filter guilds by manage guild permission
            var permissionRestGuilds =
                guilds.Where(x => (Convert.ToInt64(x.Permissions.RawValue) & manageGuild) != 0).ToList();
            foreach (var guild in permissionRestGuilds)
            {
                // check if bot is in guild
                var restGuild = await discordClient.GetGuildAsync(guild.Id);
                var inGuild = restGuild != null;

                var obj = new Guild
                {
                    Id = guild.Id.ToString(),
                    Name = guild.Name,
                    Icon = guild.IconUrl ?? "_",
                    Permissions = guild.Permissions.ToString(),
                    InGuild = inGuild
                };

                permissionGuilds.Add(obj);
            }

            return permissionGuilds.ToArray();
        }
    }
}