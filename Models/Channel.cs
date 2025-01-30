using Newtonsoft.Json;

namespace TED.API.Models
{
    public class Channel
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("position")]
        public int Position { get; set; }
    }
}
