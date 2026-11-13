using ChatApp.Common;
using ChatApp.Data;
using ChatApp.DTOs;
using ChatApp.Models;
using ChatApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Text.Json;

namespace ChatApp.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/chat")]
    public class ChatController : ControllerBase
    {
        private readonly IMessageService _messageService;
        private readonly MongoDbContext _db;
        private readonly IConfiguration _config;

        public ChatController(
            IMessageService messageService,
            MongoDbContext db,
            IConfiguration config)
        {
            _messageService = messageService;
            _db = db;
            _config = config;
        }

        // =====================================
        // NORMAL MESSAGE SEND
        // =====================================
        [HttpPost("send")]
        public async Task<IActionResult> Send(
            [FromQuery] string? senderId,
            [FromQuery] string? receiverId,
            [FromQuery] string? text,
            [FromBody] SendMessageRequestDto? body = null)
        {
            var authenticatedId = User.GetUserId();

            // Extract values preferring body then query
            var effectiveReceiverId = body?.ReceiverId ?? receiverId;
            var effectiveText = body?.Text ?? text;

            if (string.IsNullOrWhiteSpace(effectiveReceiverId) || string.IsNullOrWhiteSpace(effectiveText))
            {
                throw new BadRequestException("Receiver ID and message text are required");
            }

            // Security: Never trust client-provided senderId if it differs from authenticated user
            var result = await _messageService.SendMessageAsync(authenticatedId, effectiveReceiverId, effectiveText);
            return Ok(result);
        }

        // =====================================
        // CHAT HISTORY (Clean Route)
        // =====================================
        [HttpGet("history/{otherUserId}")]
        public async Task<IActionResult> HistorySingle(
            string otherUserId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 30)
        {
            var authenticatedId = User.GetUserId();
            var result = await _messageService.GetConversationHistoryAsync(authenticatedId, otherUserId, page, pageSize);
            return Ok(result);
        }

        // =====================================
        // CHAT HISTORY (Backward Compatibility)
        // =====================================
        [HttpGet("history/{user1}/{user2}")]
        public async Task<IActionResult> History(
            string user1,
            string user2,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 30)
        {
            var authenticatedId = User.GetUserId();

            // Security: Ensure caller is one of the participants
            if (authenticatedId != user1 && authenticatedId != user2)
            {
                throw new ForbiddenException("You are not authorized to view this conversation");
            }

            var otherUserId = authenticatedId == user1 ? user2 : user1;
            var result = await _messageService.GetConversationHistoryAsync(authenticatedId, otherUserId, page, pageSize);
            return Ok(result);
        }

        // =====================================
        // SEND STICKER
        // =====================================
        [HttpPost("send-sticker")]
        public async Task<IActionResult> SendSticker(
            [FromQuery] string? senderId,
            [FromQuery] string? receiverId,
            [FromQuery] string? stickerUrl,
            [FromBody] SendStickerRequestDto? body = null)
        {
            var authenticatedId = User.GetUserId();

            var effectiveReceiverId = body?.ReceiverId ?? receiverId;
            var effectiveStickerUrl = body?.StickerUrl ?? stickerUrl;

            if (string.IsNullOrWhiteSpace(effectiveReceiverId) || string.IsNullOrWhiteSpace(effectiveStickerUrl))
            {
                throw new BadRequestException("Receiver ID and sticker URL are required");
            }

            var result = await _messageService.SendStickerAsync(authenticatedId, effectiveReceiverId, effectiveStickerUrl);
            return Ok(result);
        }

        // =====================================
        // MARK SEEN
        // =====================================
        [HttpPost("mark-seen/{senderId}")]
        public async Task<IActionResult> MarkSeen(string senderId)
        {
            var authenticatedId = User.GetUserId();
            await _messageService.MarkSeenAsync(authenticatedId, senderId);
            return Ok(ApiResponse.Ok("Messages marked as seen"));
        }

        // =====================================
        // GET USER STICKERS
        // =====================================
        [HttpGet("stickers/{userId}")]
        public async Task<IActionResult> GetUserStickers(string userId)
        {
            var authenticatedId = User.GetUserId();
            var targetUserId = string.IsNullOrWhiteSpace(userId) ? authenticatedId : userId;

            var data = await _db.Stickers
                .Find(x => x.UserId == targetUserId)
                .SortByDescending(x => x.CreatedAt)
                .ToListAsync();

            return Ok(data);
        }

        // =====================================
        // AI STICKER GENERATE
        // =====================================
        [HttpPost("generate-sticker")]
        public async Task<IActionResult> GenerateSticker([FromQuery] string? userId, [FromQuery] string prompt)
        {
            var authenticatedId = User.GetUserId();
            var targetUserId = string.IsNullOrWhiteSpace(userId) ? authenticatedId : authenticatedId;

            if (string.IsNullOrWhiteSpace(prompt))
                throw new BadRequestException("Prompt cannot be empty");

            try
            {
                var today = DateTime.UtcNow.Date;

                var aiCount = await _db.Stickers
                    .Find(x =>
                        x.UserId == targetUserId &&
                        x.CreatedAt >= today &&
                        x.IsAiGenerated
                    )
                    .CountDocumentsAsync();

                List<string> giphyList = new();

                try
                {
                    giphyList = await GetGiphyStickers(prompt);
                }
                catch
                {
                    giphyList = new List<string>();
                }

                return Ok(new
                {
                    aiSticker = (string?)null,
                    giphyStickers = giphyList,
                    aiLimitReached = aiCount >= 5
                });
            }
            catch (Exception ex)
            {
                throw new BadRequestException(ex.Message);
            }
        }

        private async Task<List<string>> GetGiphyStickers(string prompt)
        {
            var apiKey = _config["Giphy:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey)) return new List<string>();

            using var http = new HttpClient();
            var url = $"https://api.giphy.com/v1/stickers/search?api_key={apiKey}&q={Uri.EscapeDataString(prompt)}&limit=20&rating=g";

            var response = await http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return new List<string>();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var list = new List<string>();
            if (doc.RootElement.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in dataElement.EnumerateArray())
                {
                    if (item.TryGetProperty("images", out var images) &&
                        images.TryGetProperty("original", out var original) &&
                        original.TryGetProperty("url", out var urlProp))
                    {
                        var urlStr = urlProp.GetString();
                        if (!string.IsNullOrEmpty(urlStr))
                        {
                            list.Add(urlStr);
                        }
                    }
                }
            }

            return list;
        }
    }
}
