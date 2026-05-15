using ChatApp.Data;
using ChatApp.Helpers;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace ChatApp.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly MongoDbContext _context;
        private readonly EncryptionHelper _crypto;

        public UsersController(
            MongoDbContext context,
            EncryptionHelper crypto)
        {
            _context = context;
            _crypto = crypto;
        }

        [HttpGet("{currentUserId}")]
        public async Task<IActionResult> GetUsers(string currentUserId)
        {
            var users = await _context.Users
                .Find(x => x.Id != currentUserId)
                .ToListAsync();

            var result = new List<object>();

            foreach (var user in users)
            {
                var unreadCount = await _context.Messages
                    .CountDocumentsAsync(m =>
                        m.SenderId == user.Id &&
                        m.ReceiverId == currentUserId &&
                        m.IsSeen == false);

                var lastMsg = await _context.Messages
                    .Find(m =>
                        (m.SenderId == user.Id &&
                         m.ReceiverId == currentUserId)

                         ||

                        (m.SenderId == currentUserId &&
                         m.ReceiverId == user.Id))
                    .SortByDescending(m => m.CreatedAt)
                    .FirstOrDefaultAsync();

                result.Add(new
                {
                    user.Id,
                    user.FullName,
                    user.Email,
                    user.IsOnline,
                    unreadCount,
                    lastMessage = lastMsg == null
                        ? ""
                        : _crypto.Decrypt(lastMsg.EncryptedText)
                });
            }

            return Ok(result);
        }
    }
}