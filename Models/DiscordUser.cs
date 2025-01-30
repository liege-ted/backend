using Newtonsoft.Json;

namespace TED.API.Models
{
    public class DiscordUser
    {
        [JsonProperty("id")]
        public string? Id { get; set; }
        [JsonProperty("username")]
        public string? Username { get; set; }
        [JsonProperty("avatarurl")]
        public string? AvatarUrl { get; set; }
    }
}
