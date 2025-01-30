using Newtonsoft.Json;

namespace TED.API.Models
{
    public class GuildManageProps
    {
        [JsonProperty("guild")]
        public Guild? Guild { get; set; }

        [JsonProperty("channels")]
        public Channel[]? Channels { get; set; }

        [JsonProperty("roles")]
        public Role[]? Roles { get; set; }
    }
}