using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;

namespace TED.API.Controllers.CDNService;

[Route("cdn")]
[ApiController]
public class ContentDeliveryController : BaseController
{
    // CDN is private for now
    private readonly string _cdnToken = Environment.GetEnvironmentVariable("CDN_TOKEN") ?? "cdntoken";
    private readonly string _fileStorePath = Environment.GetEnvironmentVariable("IMAGE_STORE") ?? @"C:\images";

    public ContentDeliveryController(HttpClient httpClient)
    {
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("tedBackendCache", "1.0"));
    }

    [HttpPost("upload")]
    public async Task<IActionResult> HttpPostFileAsync()
    {
        try
        {
            var key = Request.Headers.Authorization.FirstOrDefault();
            if (key is null || key != _cdnToken) return Unauthorized();
            var id = Guid.NewGuid();
            await using var fileStream = System.IO.File.OpenWrite(Path.Combine(_fileStorePath, id.ToString()));
            await Request.Body.CopyToAsync(fileStream);
            return JsonOk(id);
        }
        catch (Exception e) 
        { 
            Console.WriteLine(e);
            return JsonInternalServerError("Something went wrong!"); 
        }
    }
    
    [HttpGet]
    public async Task<IActionResult> HttpGetFileAsync(string id)
    {
        try
        {
            var file = Path.Combine(_fileStorePath, id);
            if (!System.IO.File.Exists(file)) return JsonNotFound();
            
            await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);

            // get file type from first 4 bytes
            var header = new byte[4];
            stream.Read(header, 0, header.Length);
            stream.Position = 0;

            // zip      50 4B   PK
            // png      89 50 4E 47     %PNG
            // txt
            
            var contentType = header[0] switch
            {
                0x50 when header[1] == 0x4B => "application/zip", // PK
                0x89 when header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 => "image/png", // PNG
                _ when header.All(b => b is >= 0x09 and <= 0x7E or 0x0A or 0x0D) => "text/plain", // checking if all bytes are printable
                _ => "application/octet-stream" // default
            };

            return File(stream, contentType);
        }
        catch (Exception e) 
        { 
            Console.WriteLine(e);
            return JsonInternalServerError("Something went wrong!"); 
        }    
    }
}