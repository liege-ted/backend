using StackExchange.Redis;
using System.Net.Http.Headers;

namespace TED.API.Middleware
{
    /// <summary>
    /// A custom "Sliding Window Rate Limiter" that I think I have perfected.
    /// Time complexity should be O(1) instead of O(n) like the previous implementation.
    /// </summary>
    public class RateLimitMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IDatabase _redis;

        public RateLimitMiddleware(RequestDelegate next, HttpClient httpClient, IConnectionMultiplexer muxer)
        {
            _next = next;
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("tedBackendCache", "1.0"));
            _redis = muxer.GetDatabase();
        }

        // ratelimit - subject to change but this is a small project and we will see how this does :)
        // up to 10 requests within a 3 second window
        // up to 60 requests within a 60 second window
        
        public async Task Invoke(HttpContext context)
        {
            // ip takes priority because it is guaranteed, while token may not exist at the time
            
            var ip = GetIp(context);
            var token = GetToken(context);
            
            if (ip is null) return;

            // ip ratelimiting
            if (await IsRateLimitedAsync($"ratelimit_ip_{ip}", 10, TimeSpan.FromSeconds(3)) ||
                await IsRateLimitedAsync($"ratelimit_ip_{ip}", 60, TimeSpan.FromSeconds(60)))
            {
                context.Response.StatusCode = 429;
                await context.Response.WriteAsync("Too many requests!");
                return;
            }
            
            // token ratelimiting
            if (token is not null)
            {
                if (await IsRateLimitedAsync($"ratelimit_token_{token}", 10, TimeSpan.FromSeconds(3)) ||
                    await IsRateLimitedAsync($"ratelimit_token_{token}", 60, TimeSpan.FromSeconds(60)))
                {
                    context.Response.StatusCode = 429;
                    await context.Response.WriteAsync("Too many requests!");
                    return;
                }
            }
            
            await _next(context);
        }

        /// <summary>
        /// Attempts to get the IP address of the client from the request headers
        /// </summary>
        /// <returns>An IP address</returns>
        private static string? GetIp(HttpContext context)
        {
            var req = context.Request;
            return req.Headers["Cf-Connecting-Ip"].ToString()?.Trim() ??
                   req.Headers["X-Forwarded-For"].ToString()?.Trim() ??
                   req.HttpContext.Connection.RemoteIpAddress?.ToString() ??
                   null; // some real issues if this is returning null lol
        }

        /// <summary>
        /// Attempts to get the token from the request headers
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private static string? GetToken(HttpContext context)
        {
            var req = context.Request;
            if (req.Headers.TryGetValue("Authorization", out var headerToken))
                return headerToken.ToString().Trim();
            if (req.Cookies.TryGetValue("token", out var cookieToken))
                return cookieToken.Trim();
            return null;
        }
        
        /// <summary>
        /// Saves and/or increments provided key and checks if the request exceeds ratelimit
        /// </summary>
        /// <returns>True if ratelimit has met; false otherwise</returns>
        private async Task<bool> IsRateLimitedAsync(string key, int limit, TimeSpan window)
        {
            var count = await _redis.StringIncrementAsync(key);
            
            if (count == 1)
                await _redis.KeyExpireAsync(key, window);
            
            return count > limit;
        }
       
    }
}
