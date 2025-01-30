using MongoDB.Driver;
using Newtonsoft.Json;
using TED.API.Models;
using TED.Models;

namespace TED.API.Services;

/// <summary>
/// This was a originally just a 60 second timer, but I have optimized it to only
/// run when there are sessions that need to be refreshed. Thank you to microsoft docs and ChatGPT!
/// </summary>
public class TokenRefreshService : BackgroundService
{
    private readonly string _clientId = Environment.GetEnvironmentVariable("CLIENT_ID")!;
    private readonly string _clientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET")!;
    private readonly string _redirectUrl = Environment.GetEnvironmentVariable("OAUTH_REDIRECT_URL") ?? "http://localhost:3001/oauth";
    
    private readonly HttpClient _httpClient = new();

    // https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-9.0&tabs=visual-studio
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // while cancel has not been requested
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // get all expiring sessions and attempt to refresh tokens
                var sessions = await GetExpiringSessionTokensAsync();
                if (sessions is null) continue;

                // refresh tokens in parallel
                await Task.WhenAll(sessions.Select(RefreshTokenAsync));
            }
            catch
            {
                Console.WriteLine("There was an error refreshing the tokens!");
            }

            // wait for 1 minute before trying again
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    /// <summary>
    /// Attempts to get all the tokens that are expiring in the next hour
    /// </summary>
    /// <returns>A list of WebSession objects that have expiring access tokens</returns>
    private async Task<List<WebSession>?> GetExpiringSessionTokensAsync()
        => await MongoService.WebSessions.Find(x => x.ExpireDate.AddHours(-1) < DateTime.UtcNow).ToListAsync();

    /// <summary>
    /// Attempts to refresh the token of a session
    /// </summary>
    private async Task RefreshTokenAsync(WebSession session)
    {
        Dictionary<string, string> keyValues = new()
        {
            { "client_id", _clientId },
            { "client_secret", _clientSecret },
            { "grant_type", "refresh_token" },
            { "refresh_token", session.RefreshToken! },
            { "redirect_uri", _redirectUrl }
        };
        
        var content = new FormUrlEncodedContent(keyValues);
        const string postUrl = "https://discord.com/api/v10/oauth2/token";
        var postResponse = await _httpClient.PostAsync(postUrl, content);
        
        if (!postResponse.IsSuccessStatusCode) return;
        var responseString = await postResponse.Content.ReadAsStringAsync();
        var response = JsonConvert.DeserializeObject<OAuthResponse>(responseString);
        
        if (session.UserId is null || response is null)
            return;
        
        var jwt = Jwt.GenerateToken(session.UserId, response.AccessToken);
        session.AccessToken = response.AccessToken;
        session.RefreshToken = response.RefreshToken;
        session.ExpireDate = DateTime.Now.AddSeconds(response.ExpiresIn);
        session.UserSecret = jwt;
        await session.SaveAsync();
    }
}