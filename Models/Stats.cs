using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace TED.API.Models
{
    public class Stats
    {
        [JsonProperty("guildCount")]
        public int GuildCount { get; set; }
        [JsonProperty("userCount")]
        public int UserCount { get; set; }
        [JsonProperty("actionCount")]
        public int ActionCount { get; set; }
    }
}
