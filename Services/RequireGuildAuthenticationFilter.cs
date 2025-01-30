using Discord.Rest;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TED.API.Extensions;

namespace TED.API.Services
{
    public class RequireGuildAuthenticationFilter(DiscordRestClient discordClient) : ActionFilterAttribute
    {
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // try get guild id from route
            if (!context.RouteData.Values.TryGetValue("id", out var guildIdObject))
            {
                context.Result = new NotFoundResult();
                return;
            }
            
            // try parse guild id to string
            var guildid = Convert.ToString(guildIdObject);
            if (guildid is null)
            {
                context.Result = new NotFoundResult();
                return;
            }
            
            // auth user with guild
            var result = await context.HttpContext.Request.AuthenticateUserWithGuildAsync(discordClient, guildid);
            if (result)
            {
                await next();
                return;
            }

            // return not found if user is not authenticated
            context.Result = new NotFoundResult();
        }
    }
}