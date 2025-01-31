using Discord.Rest;
using Newtonsoft.Json;
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
        
        /// <summary>
        /// Gets the guilds the user has permission to manage; HttpClient must be in an authorized state
        /// </summary>
        /// <param name="client">MUST BE POST AUTHORIZATION</param>
        /// <returns>An array of Guild objects</returns>
        public static async Task<Guild[]?> GetPermissionGuildsAsync(this HttpClient client)
        {
            var getResult = await client.GetAsync("https://discord.com/api/v10/users/@me/guilds");
            
            var permissionGuilds = new List<Guild>();

            var guilds = JsonConvert.DeserializeObject<Guild[]>(await getResult.Content.ReadAsStringAsync());
            if (guilds is null) return null;
            
            foreach (var guild in guilds)
            {
                var permissions = Convert.ToInt64(guild.Permissions);
                // const int manageMessages = 14;
                const int manageGuild = 32;
        
                if ((permissions & manageGuild) != 0)
                    permissionGuilds.Add(guild);
            }

            return permissionGuilds.ToArray();
        }

    }
}