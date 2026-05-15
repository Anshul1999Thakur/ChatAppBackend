using ChatApp.Models;
using MongoDB.Driver;

namespace ChatApp.Data
{
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;

        public MongoDbContext(IConfiguration config)
        {
            var client = new MongoClient(
                config["MongoDbSettings:ConnectionString"]);

            _database = client.GetDatabase(
                config["MongoDbSettings:DatabaseName"]);
        }

        public IMongoCollection<User> Users =>
            _database.GetCollection<User>("Users");

        public IMongoCollection<Message> Messages =>
            _database.GetCollection<Message>("Messages");

        public IMongoCollection<Sticker> Stickers =>
            _database.GetCollection<Sticker>("Stickers");
    }
}