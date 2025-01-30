using Microsoft.AspNetCore.Mvc;
using TED.API.Services;

namespace TED.API.Attributes
{
    public class RequireGuildAuthenticationAttribute() : TypeFilterAttribute(typeof(RequireGuildAuthenticationFilter));
}