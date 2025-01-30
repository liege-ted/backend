using TED.Models;

namespace TED.API.Models
{
    public class ReasonShortcutComparer : IEqualityComparer<ReasonShortcut>
    {
        public bool Equals(ReasonShortcut? x, ReasonShortcut? y)
            => x?.GuildId == y?.GuildId && x?.Reason == y?.Reason && x?.Shortcut == y?.Shortcut;

        public int GetHashCode(ReasonShortcut obj)
            => obj.GuildId.GetHashCode() ^ obj.Reason.GetHashCode() ^ obj.Shortcut.GetHashCode();
    }
}
