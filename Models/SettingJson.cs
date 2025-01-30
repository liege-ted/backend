using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using TED.Additions.Enums;
using TED.API.Enums;

namespace TED.API.Models
{
    public class SettingJson
    {
        [JsonProperty("guildid")]
        public string? GuildId { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("setting")]
        public Setting Setting { get; set; }
        [JsonProperty("config")]
        public MockConfig? Config { get; set; }

        public class MockConfig
        {
            [JsonProperty("isEnabled")] 
            public bool IsEnabled { get; set; }
            [JsonProperty("action")]
            public string? Action { get; set; }
            [JsonProperty("slowmodeInterval")]
            public string? SlowmodeInterval { get; set; }
        }
    }
}
