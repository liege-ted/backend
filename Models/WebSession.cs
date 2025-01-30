using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace TED.API.Models
{
    [BsonIgnoreExtraElements]
    public class WebSession
    {
        [BsonElement("userid")]
        public string? UserId { get; init; }
        [BsonElement("accessToken")]
        public string? AccessToken { get; set; }
        [BsonElement("refreshToken")]
        public string? RefreshToken { get; set; }
        [BsonElement("expireDate")]
        public DateTime ExpireDate { get; set; }
        [BsonElement("userSecret")]
        public string? UserSecret { get; set; }

        public async Task SaveAsync()
            => await Services.MongoService.WebSessions.ReplaceOneAsync(x => x.UserId == UserId, this, new ReplaceOptions() { IsUpsert = true });
    }
}
