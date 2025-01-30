using Newtonsoft.Json;
using TED.Models;
using TED.Additions.Enums;

namespace TED.API.Models
{
    public class CensorComparer : IEqualityComparer<Censor>
    {
        public bool Equals(Censor? x, Censor? y)
            => x?.GuildId == y?.GuildId && x?.Term == y?.Term;

        public int GetHashCode(Censor obj)
            => obj.GuildId.GetHashCode() ^ obj.Term.GetHashCode();
    }

    public class BulkCensor
    {
        [JsonProperty("guildid")]
        public string? GuildId { get; set; }
        [JsonProperty("terms")]
        public string[]? Terms { get; set; }
        [JsonProperty("action")]
        public AutoAction? Action { get; set; }
    }
}
