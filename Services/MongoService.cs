using MongoDB.Driver;
using TED.API.Models;

namespace TED.API.Services
{
    public static class MongoService
    {
        private static readonly MongoClient Client = new(Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING") ?? "mongodb://localhost:27017");

        private static IMongoDatabase Database
            => Client.GetDatabase("TED");
        
        public static IMongoCollection<WebSession> WebSessions 
            => Database.GetCollection<WebSession>("web-sessions");
    }
}
