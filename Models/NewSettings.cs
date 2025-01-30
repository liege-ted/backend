using Newtonsoft.Json;
using TED.API.Enums;
using TED.Additions.Enums;

namespace TED.API.Models
{
    class NewSettingsJson
    {
        [JsonProperty("setting")]
        public Setting Setting { get; set; }
        [JsonProperty("isEnabled")]
        public bool IsEnabled { get; set; }
        [JsonProperty("action")]
        public AutoAction Action { get; set; }
        [JsonProperty("slowmodeInterval")]
        public string SlowmodeInterval { get; set; }
    }
}
