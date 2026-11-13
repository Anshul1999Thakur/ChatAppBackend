using ChatApp.Data;
using ChatApp.Models;
using MongoDB.Driver;

namespace ChatApp.Repositories
{
    public class GroupMemberRepository : IGroupMemberRepository
    {
        private readonly MongoDbContext _context;

        public GroupMemberRepository(MongoDbContext context)
        {
            _context = context;
        }

        public async Task AddMemberAsync(GroupMember member)
        {
            await _context.GroupMembers.InsertOneAsync(member);
        }

        public async Task AddMembersAsync(IEnumerable<GroupMember> members)
        {
            var list = members.ToList();
            if (list.Count > 0)
            {
                await _context.GroupMembers.InsertManyAsync(list);
            }
        }

        public async Task<GroupMember?> GetMembershipAsync(string groupId, string userId)
        {
            return await _context.GroupMembers.Find(m => m.GroupId == groupId && m.UserId == userId).FirstOrDefaultAsync();
        }

        public async Task<List<GroupMember>> GetMembersByGroupIdAsync(string groupId)
        {
            return await _context.GroupMembers.Find(m => m.GroupId == groupId).ToListAsync();
        }

        public async Task<List<GroupMember>> GetMembershipsByUserIdAsync(string userId)
        {
            return await _context.GroupMembers.Find(m => m.UserId == userId).ToListAsync();
        }

        public async Task<int> CountMembersAsync(string groupId)
        {
            return (int)await _context.GroupMembers.CountDocumentsAsync(m => m.GroupId == groupId);
        }

        public async Task RemoveMemberAsync(string groupId, string userId)
        {
            await _context.GroupMembers.DeleteOneAsync(m => m.GroupId == groupId && m.UserId == userId);
        }

        public async Task UpdateRoleAsync(string groupId, string userId, string newRole)
        {
            var update = Builders<GroupMember>.Update.Set(m => m.Role, newRole);
            await _context.GroupMembers.UpdateOneAsync(m => m.GroupId == groupId && m.UserId == userId, update);
        }

        public async Task DeleteAllMembersByGroupIdAsync(string groupId)
        {
            await _context.GroupMembers.DeleteManyAsync(m => m.GroupId == groupId);
        }
    }
}

