using StackExchange.Redis;

namespace TED.API.Extensions
{
    public static class UpdateCache
    {
        /// <summary>
        /// Updates cache with new json string
        /// </summary>
        public static async Task UpdateCacheAsync(this IDatabase redis, string key, string newJson)
        {
            try { await redis.StringSetAsync(key, newJson, TimeSpan.FromDays(7)); }
            catch { await redis.KeyDeleteAsync(key); }
        }
    }
}
