using Discord;
using Discord.Rest;
using StackExchange.Redis;
using TED.API.Middleware;
using TED.API.Services;

namespace TED.API
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateSlimBuilder(args);
            
            builder.Services.AddCors(options =>
            {
                // half of these are not necessary at all and don't even make sense
                options.AddPolicy("CorsPolicy",
                    builder => builder.WithOrigins([
                            "http://localhost:3001",
                            "http://localhost:3000",
                            "https://api.liege.dev",
                            "http://api.liege.dev",
                            "https://ted.liege.dev",
                            "http://ted.liege.dev",
                            "https://discord.com",
                            "https://ted.liege"])
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials());
            });

            builder.Services.AddControllers().AddNewtonsoftJson(options =>
            {
                options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
                options.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;
            });

            // add discord rest client to services
            builder.Services.AddSingleton<DiscordRestClient>();
            builder.Services.AddSingleton<IConnectionMultiplexer>(await ConnectionMultiplexer.ConnectAsync(Environment.GetEnvironmentVariable("REDIS_CONNECT") ?? "localhost"));
            builder.Services.AddHttpClient();

            var tokenRefreshService = new TokenRefreshService();
            
            var app = builder.Build();
            
            app.UseCors("CorsPolicy");

            // new discord rest client
            await app.Services.GetRequiredService<DiscordRestClient>().LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("BOT_TOKEN"));
            
            app.UseMiddleware<ParameterValidationMiddleware>();
            app.UseMiddleware<RateLimitMiddleware>();
            // app.UseMiddleware<AuthenticationMiddleware>();
            
            app.MapControllers();

            await app.RunAsync();
        }
    }
}
