using MongoDB.Driver;
using TED.Models;

namespace TED.API.Extensions;

public abstract class GetApiSession
{
    /// <summary>
    /// Attempts the ApiKey object from the provided apikey; used for the public facing API, not the web interface.
    /// </summary>
    /// <returns>The ApiKey object containing all information related to the provided api key</returns>
    public static async Task<ApiKey?> TryGetUserFromApiKeyAsync(string apiKey)
        => await TED.Services.MongoService.ApiKeys.Find(x => x.Token == apiKey).FirstOrDefaultAsync();
}