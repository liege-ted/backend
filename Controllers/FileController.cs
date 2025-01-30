/*
 * MOVED TO CDN SERVICE; THIS FILE IS ARCHIVED
 */

/*using Microsoft.AspNetCore.Mvc;
using TED.Models;

namespace TED.API.Controllers
{
    [ApiController]
    [Route("file")]
    public class FileController : BaseController
    {
        [HttpGet]
        [RequiredQueryParam("id")]
        public IActionResult HttpGetFileAsync(string id)
        {
            var massEvent = MassEvent.GetMassEvent(id);

            //var dir = Environment.GetEnvironmentVariable("LISTEN_FILE_DIR");          
            //var file = System.IO.File.ReadAllText($"{dir}/{massEvent.GuildId}-{id}.txt");

            if (massEvent == null)
            {
                return JsonNotFound("Could not find this file!");
            }

            var userContent = $"{string.Join("\n", massEvent.UserIds)}";
            Response.ContentType = "text/plain";
            return JsonOk(userContent);
        }
    }
}*/
