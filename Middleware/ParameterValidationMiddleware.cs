using Microsoft.AspNetCore.Mvc.Controllers;
using Newtonsoft.Json;
using TED.API.Attributes;

namespace TED.API.Middleware
{
    /// <summary>
    /// This class was not written entirely by me, but I did modify it to fit this projects needs
    /// </summary>
    public class ParameterValidationMiddleware(RequestDelegate next)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            // get the endpoint and controller action descriptor
            var endpoint = context.GetEndpoint();
            var controllerActionDescriptor = endpoint?.Metadata.GetMetadata<ControllerActionDescriptor>();
            
            // if the endpoint is not a controller action, return
            if (controllerActionDescriptor != null)
            {
                // get the required query parameters
                var requiredQueryParams = controllerActionDescriptor.MethodInfo
                    .GetCustomAttributes(typeof(RequiredQueryParamAttribute), false)
                    .Cast<RequiredQueryParamAttribute>()
                    .Select(attr => attr.ParamName);

                // if the request is missing any required query parameters, return
                foreach (var param in requiredQueryParams)
                {
                    if (context.Request.Query.ContainsKey(param)) continue;
                    
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    context.Response.ContentType = "application/json";
                    var response = new
                    {
                        code = 400,
                        message = $"Bad request: the '{param}' query parameter is missing!"
                    };
                    await context.Response.WriteAsync(JsonConvert.SerializeObject(response));
                    return;
                }

                // get the required body parameters
                var requiredBodyParams = controllerActionDescriptor.MethodInfo
                    .GetCustomAttributes(typeof(RequiredBodyParamAttribute), false)
                    .Cast<RequiredBodyParamAttribute>()
                    .Select(attr => attr.ParamName);

                // if the request is a json request and there are required body parameters
                if (context.Request.ContentType == "application/json" && requiredBodyParams.Any())
                {
                    // read the body and parse it
                    context.Request.EnableBuffering();
                    var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
                    context.Request.Body.Position = 0;

                    var jsonBody = System.Text.Json.JsonDocument.Parse(body).RootElement;

                    // if the request is missing any required body parameters, return
                    foreach (var param in requiredBodyParams)
                    {
                        if (jsonBody.TryGetProperty(param, out _)) continue;
                        
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        context.Response.ContentType = "application/json";
                        var response = new
                        {
                            code = 400,
                            message = $"Bad request: the '{param}' body parameter is missing!"
                        };
                        await context.Response.WriteAsync(JsonConvert.SerializeObject(response));
                        return;
                    }
                }
            }

            await next(context);
        }
    }
}