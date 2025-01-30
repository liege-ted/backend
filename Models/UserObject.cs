using Newtonsoft.Json;

namespace TED.API.Models;

public class UserObject
{
    [JsonProperty("user_id")]
    public string? UserId { get; set; }
    [JsonProperty("username")]
    public string? Username { get; set; }
    [JsonProperty("avatar_url")]
    public string? AvatarUrl { get; set; }
}