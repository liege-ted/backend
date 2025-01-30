using Newtonsoft.Json;

namespace TED.API.Models
{
    public class Role
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("permissions")]
        public string? Permissions { get; set; }
    }
}
