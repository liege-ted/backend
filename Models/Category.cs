using Newtonsoft.Json;

namespace TED.API.Models
{
    public class Category
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("channels")]
        public Channel[]? Channels { get; set; }
    }
}
