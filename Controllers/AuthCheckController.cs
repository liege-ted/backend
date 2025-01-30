using Discord.Rest;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TED.API.Extensions;

namespace TED.API.Controllers;

[ApiController]
[Route("authcheck")]
public class AuthCheckController(DiscordRestClient discordClient) : BaseController
{
    /// <summary>
    /// Request is intended to provide a detailed response on the validity of the provided token
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> HttpAuthCheckAsync([FromRoute]string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            var session = await GetCreateWebSession.TryGetSessionFromRequestAsync(Request);
            return session == null 
                ? JsonForbidden("The provided token is invalid.") 
                : JsonOk("The provided token is valid.");
        }

        bool hasPerms;
        try { hasPerms = await Request.AuthenticateUserWithGuildAsync(discordClient, id); }
        catch { hasPerms = false; }
        return !hasPerms 
            ? JsonForbidden("The provided token cannot be authenticated because the user does not have valid permissions on the guild!") 
            : JsonOk("The provided token is valid and the user has valid permissions on the guild!");
    }
}