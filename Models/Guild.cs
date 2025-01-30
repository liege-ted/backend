using Newtonsoft.Json;

namespace TED.API.Models
{
    public class Guild
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("icon")]
        public string? Icon { get; set; }

        [JsonProperty("permissions")]
        public string? Permissions { get; set; }

        [JsonProperty("inGuild")]
        public bool InGuild { get; set; }

        [JsonProperty("memberCount")]
        public int MemberCount { get; set; }

        [JsonProperty("owner")]
        public string? Owner { get; set; }
    }
}
