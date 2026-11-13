using ChatApp.Data;
using ChatApp.Models;
using MongoDB.Driver;

namespace ChatApp.Repositories
{
    public class GroupRepository : IGroupRepository
    {
        private readonly MongoDbContext _context;

        public GroupRepository(MongoDbContext context)
        {
            _context = context;
        }

        public async Task CreateAsync(ChatGroup group)
        {
            await _context.ChatGroups.InsertOneAsync(group);
        }

        public async Task<ChatGroup?> GetByIdAsync(string id)
        {
            return await _context.ChatGroups.Find(g => g.Id == id && !g.IsDeleted).FirstOrDefaultAsync();
        }

        public async Task<List<ChatGroup>> GetByIdsAsync(IEnumerable<string> ids)
        {
            var idList = ids.Distinct().ToList();
            if (idList.Count == 0) return new List<ChatGroup>();

            return await _context.ChatGroups
                .Find(g => idList.Contains(g.Id) && !g.IsDeleted)
                .ToListAsync();
        }

        public async Task<bool> ExistsByNameForUserAsync(string name, string createdBy)
        {
            return await _context.ChatGroups.Find(g =>
                g.Name.ToLower() == name.Trim().ToLower() &&
                g.CreatedBy == createdBy &&
                !g.IsDeleted).AnyAsync();
        }

        public async Task UpdateAsync(ChatGroup group)
        {
            group.UpdatedAt = DateTime.UtcNow;
            await _context.ChatGroups.ReplaceOneAsync(g => g.Id == group.Id, group);
        }

        public async Task SoftDeleteAsync(string id)
        {
            var update = Builders<ChatGroup>.Update
                .Set(g => g.IsDeleted, true)
                .Set(g => g.DeletedAt, DateTime.UtcNow);

            await _context.ChatGroups.UpdateOneAsync(g => g.Id == id, update);
        }
    }
}

