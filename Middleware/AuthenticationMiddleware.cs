using System.Text.RegularExpressions;
using Discord.Rest;
using TED.API.Extensions;

namespace TED.API.Middleware
{
    [Obsolete("This middleware is no longer used as it was replaced by the RequireGuildAuthentication attribute")]
    public class AuthenticationMiddleware(RequestDelegate next, DiscordRestClient botClient)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            // check for guildid in path, use it if found
            var guildIdMatch = Regex.Match(context.Request.PathBase, @"guilds\/\d{17,19}");
            if (guildIdMatch.Success)
            {
                var guildId = guildIdMatch.Value.Split("/").Last();
                await context.Request.AuthenticateUserWithGuildAsync(botClient, guildId);
            }
            
            await next(context);
        }
    }
}
