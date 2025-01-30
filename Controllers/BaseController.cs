using Microsoft.AspNetCore.Mvc;

namespace TED.API.Controllers
{
    [ApiController]
    public class BaseController : ControllerBase
    {
        // custom methods for returning json responses
        
        protected IActionResult JsonOk(object? data = null) => Ok(data);

        public IActionResult JsonBadRequest(string message = "Invalid request")
            => BadRequest(new { code = 400, error = message });
        
        public IActionResult JsonNotFound(string message = "Resource not found")
            => NotFound(new { code = 404, error = message });

        protected IActionResult JsonInternalServerError(string message = "Internal server error")
            => StatusCode(500, new { code = 500, error = message });
        
        protected IActionResult JsonForbidden(string message = "Forbidden")
            => StatusCode(403, new { code = 403, error = message });
    }
}