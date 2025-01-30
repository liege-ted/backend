using MongoDB.Driver;
using TED.Additions;
using TED.Models.GuildSettingModels;
using TED.Services;

namespace TED.API.Extensions
{
    public class DatabaseExtensions
    {
        public static async Task SetPremiumStatusAsync(string guildid, bool isPremium, string subid)
        {
            var gs = await MongoService.GuildSettings.Find(x => x.GuildId == guildid).FirstOrDefaultAsync();
            if (gs.Premium is not null)
            {
                gs.Premium.IsPremium = isPremium;
                gs.Premium.SubscriptionId = subid;
                
                await gs.ModifyAsync(x =>
                {
                    x.Premium.IsPremium = true;
                    x.Premium.SubscriptionId = subid;
                });
            }
        }

        public static async Task IncrementPaymentAsync(string guildid, double amount = 2.99)
        {
            var gs = await MongoService.GuildSettings.Find(x => x.GuildId == guildid).FirstOrDefaultAsync();
            if (gs.Premium is not null)
            {
                gs.Premium.TotalPaid += amount;
                gs.Premium.LastPaid = DateTime.Now;
                
                await gs.ModifyAsync(x =>
                {
                    x.Premium.TotalPaid += amount;
                    x.Premium.LastPaid = DateTime.Now;
                });
            }
        }

        public static async Task<Premium?> GetOrCreatePremiumAsync(string guildid, string subid)
        {
            var gs = await MongoService.GuildSettings.Find(x => x.GuildId == guildid).FirstOrDefaultAsync();
            if (gs != null) return gs.Premium;
            
            var premium = new Premium { SubscriptionId = subid };
            return premium;
        }

        public static async Task<Premium?> GetPremiumBySubscriptionIdAsync(string subid)
        {
            var gs = await MongoService.GuildSettings.Find(x => x.Premium != null && x.Premium.SubscriptionId == subid).FirstOrDefaultAsync();
            return gs.Premium;
        }
    }
}
