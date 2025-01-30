using TED.Models;

namespace TED.API.Models
{
    public class RegexComparor : IEqualityComparer<RegexStatement>
    {
        public bool Equals(RegexStatement? x, RegexStatement? y)
            => x?.GuildId == y?.GuildId && x?.Statement == y?.Statement;

        public int GetHashCode(RegexStatement obj)
            => obj.GuildId.GetHashCode() ^ obj.Statement.GetHashCode();
    }
}
