using MongoDB.Driver;
using TED.Models;
using TED.Services;

namespace TED.API.Extensions
{
    public class GetGuildSettings
    {
        public GuildSettings GetDatabaseGuildSettings(string id)
            => MongoService.GuildSettings.Find(g => g.GuildId == id).FirstOrDefault();
    }
}
