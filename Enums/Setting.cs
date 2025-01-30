using System.Runtime.Serialization;

namespace TED.API.Enums
{
    public enum Setting
    {
        [EnumMember(Value = "spam")]
        Spam,
        [EnumMember(Value = "scam")]
        Scam,
        [EnumMember(Value = "invite")]
        Invite,
        [EnumMember(Value = "dox")]
        DOX,
        [EnumMember(Value = "ai")]
        AI,
        [EnumMember(Value = "raid")]
        Raid,
    }
}
