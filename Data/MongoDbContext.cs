using ChatApp.Models;
using MongoDB.Driver;

namespace ChatApp.Data
{
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;

        public MongoDbContext(IConfiguration config)
        {
            var connectionString = config["MongoDbSettings:ConnectionString"]
                ?? Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING")
                ?? "mongodb://localhost:27017";

            var databaseName = config["MongoDbSettings:DatabaseName"]
                ?? "ChatAppDb";

            var client = new MongoClient(connectionString);
            _database = client.GetDatabase(databaseName);
        }

        public IMongoCollection<User> Users =>
            _database.GetCollection<User>("Users");

        public IMongoCollection<Message> Messages =>
            _database.GetCollection<Message>("Messages");

        public IMongoCollection<Sticker> Stickers =>
            _database.GetCollection<Sticker>("Stickers");

        public IMongoCollection<ChatGroup> ChatGroups =>
            _database.GetCollection<ChatGroup>("ChatGroups");

        public IMongoCollection<GroupMember> GroupMembers =>
            _database.GetCollection<GroupMember>("GroupMembers");

        public IMongoCollection<GroupMessage> GroupMessages =>
            _database.GetCollection<GroupMessage>("GroupMessages");

        public IMongoCollection<RefreshToken> RefreshTokens =>
            _database.GetCollection<RefreshToken>("RefreshTokens");

        public IMongoCollection<ConversationSetting> ConversationSettings =>
            _database.GetCollection<ConversationSetting>("ConversationSettings");

        public async Task InitializeIndexesAsync()
        {
            try
            {
                // Users: Unique email
                var userEmailIndex = new CreateIndexModel<User>(
                    Builders<User>.IndexKeys.Ascending(u => u.Email),
                    new CreateIndexOptions { Unique = true });
                await Users.Indexes.CreateOneAsync(userEmailIndex);

                // Messages: Sender/Receiver/CreatedAt compound
                var messageCompoundIndex = new CreateIndexModel<Message>(
                    Builders<Message>.IndexKeys
                        .Ascending(m => m.SenderId)
                        .Ascending(m => m.ReceiverId)
                        .Descending(m => m.CreatedAt));
                await Messages.Indexes.CreateOneAsync(messageCompoundIndex);

                var messageReceiverIndex = new CreateIndexModel<Message>(
                    Builders<Message>.IndexKeys
                        .Ascending(m => m.ReceiverId)
                        .Ascending(m => m.IsSeen));
                await Messages.Indexes.CreateOneAsync(messageReceiverIndex);

                // Messages: TTL Index on ExpiresAt
                var messageTtlIndex = new CreateIndexModel<Message>(
                    Builders<Message>.IndexKeys.Ascending(m => m.ExpiresAt),
                    new CreateIndexOptions { ExpireAfter = TimeSpan.Zero, Sparse = true });
                await Messages.Indexes.CreateOneAsync(messageTtlIndex);

                // ChatGroups: IsDeleted and CreatedBy
                var groupDeletedIndex = new CreateIndexModel<ChatGroup>(
                    Builders<ChatGroup>.IndexKeys.Ascending(g => g.IsDeleted));
                await ChatGroups.Indexes.CreateOneAsync(groupDeletedIndex);

                // GroupMembers: Compound unique on GroupId + UserId
                var memberUniqueIndex = new CreateIndexModel<GroupMember>(
                    Builders<GroupMember>.IndexKeys
                        .Ascending(m => m.GroupId)
                        .Ascending(m => m.UserId),
                    new CreateIndexOptions { Unique = true });
                await GroupMembers.Indexes.CreateOneAsync(memberUniqueIndex);

                var memberUserIndex = new CreateIndexModel<GroupMember>(
                    Builders<GroupMember>.IndexKeys.Ascending(m => m.UserId));
                await GroupMembers.Indexes.CreateOneAsync(memberUserIndex);

                // GroupMessages: Compound on GroupId + CreatedAt
                var groupMessageIndex = new CreateIndexModel<GroupMessage>(
                    Builders<GroupMessage>.IndexKeys
                        .Ascending(m => m.GroupId)
                        .Descending(m => m.CreatedAt));
                await GroupMessages.Indexes.CreateOneAsync(groupMessageIndex);

                // GroupMessages: TTL Index on ExpiresAt
                var groupMessageTtlIndex = new CreateIndexModel<GroupMessage>(
                    Builders<GroupMessage>.IndexKeys.Ascending(m => m.ExpiresAt),
                    new CreateIndexOptions { ExpireAfter = TimeSpan.Zero, Sparse = true });
                await GroupMessages.Indexes.CreateOneAsync(groupMessageTtlIndex);

                // RefreshTokens: Token and UserId
                var refreshTokenIndex = new CreateIndexModel<RefreshToken>(
                    Builders<RefreshToken>.IndexKeys.Ascending(t => t.Token));
                await RefreshTokens.Indexes.CreateOneAsync(refreshTokenIndex);

                // ConversationSettings: Compound on User1Id + User2Id
                var conversationSettingIndex = new CreateIndexModel<ConversationSetting>(
                    Builders<ConversationSetting>.IndexKeys
                        .Ascending(s => s.User1Id)
                        .Ascending(s => s.User2Id),
                    new CreateIndexOptions { Unique = true });
                await ConversationSettings.Indexes.CreateOneAsync(conversationSettingIndex);
            }
            catch
            {
                // Indexes might already exist or running in offline mode
            }
        }
    }
}
