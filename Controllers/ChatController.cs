using ChatApp.Data;
using ChatApp.Helpers;
using ChatApp.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Text;
using System.Text.Json;

namespace ChatApp.Controllers
{
    [ApiController]
    [Route("api/chat")]
    public class ChatController : ControllerBase
    {
        private readonly MongoDbContext _db;
        private readonly EncryptionHelper _crypto;
        private readonly IConfiguration _config;

        public ChatController(
            MongoDbContext db,
            EncryptionHelper crypto,
            IConfiguration config)
        {
            _db = db;
            _crypto = crypto;
            _config = config;
        }

        // =====================================
        // NORMAL MESSAGE SEND
        // =====================================
        [HttpPost("send")]
        public async Task<IActionResult> Send(
            string senderId,
            string receiverId,
            string text)
        {
            var msg = new Message
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                EncryptedText = _crypto.Encrypt(text)
            };

            await _db.Messages.InsertOneAsync(msg);

            return Ok("Message Sent");
        }

        // =====================================
        // CHAT HISTORY
        // =====================================
        [HttpGet("history/{user1}/{user2}")]
        public async Task<IActionResult> History(
            string user1,
            string user2,
            int page = 1,
            int pageSize = 30)
        {
            var data = await _db.Messages
                .Find(x =>
                    (x.SenderId == user1 &&
                     x.ReceiverId == user2) ||

                    (x.SenderId == user2 &&
                     x.ReceiverId == user1))
                .SortByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            data.Reverse();

            var result = data.Select(x => new
            {
                x.Id,
                x.SenderId,
                x.ReceiverId,

                Text = _crypto.Decrypt(
                    x.EncryptedText),

                x.StickerUrl,
                x.IsSticker,
                x.CreatedAt,
                x.IsSeen,
                x.IsDelivered
            });

            return Ok(result);
        }

        // =====================================
        // AI STICKER GENERATE
        // =====================================
        [HttpPost("generate-sticker")]
        public async Task<IActionResult> GenerateSticker(string userId, string prompt)
        {
            try
            {
                var today = DateTime.UtcNow.Date;

                // =========================
                // AI DAILY COUNT
                // =========================
                var aiCount = await _db.Stickers
                    .Find(x =>
                        x.UserId == userId &&
                        x.CreatedAt >= today &&
                        x.IsAiGenerated
                    )
                    .CountDocumentsAsync();

                string? aiImageUrl = null;

                List<string> giphyList = new();

                // =========================
                // FIRST LOAD GIPHY
                // =========================
                try
                {
                    giphyList =
                        await GetGiphyStickers(prompt);
                }
                catch
                {
                    giphyList = new List<string>();
                }

                // =========================
                // THEN TRY AI
                // =========================
                //if (aiCount < 5)
                //{
                //    try
                //    {
                //        using var http = new HttpClient();

                //        http.Timeout =
                //            TimeSpan.FromSeconds(45);

                //        var encodedPrompt =
                //            Uri.EscapeDataString(
                //                $"funny meme sticker of {prompt}, cartoon sticker, whatsapp sticker style, transparent background, clean cutout, centered object"
                //            );

                //        var response =
                //            await http.GetAsync(
                //                $"https://image.pollinations.ai/prompt/{encodedPrompt}?width=768&height=768&model=flux&nologo=true"
                //            );

                //        if (response.IsSuccessStatusCode)
                //        {
                //            var bytes =
                //                await response.Content.ReadAsByteArrayAsync();

                //            var fileName =
                //                $"{Guid.NewGuid()}.png";

                //            var folder =
                //                Path.Combine(
                //                    Directory.GetCurrentDirectory(),
                //                    "wwwroot",
                //                    "stickers"
                //                );

                //            if (!Directory.Exists(folder))
                //            {
                //                Directory.CreateDirectory(folder);
                //            }

                //            var path =
                //                Path.Combine(folder, fileName);

                //            var cleanBytes =
                //                await RemoveBackground(bytes);

                //            await System.IO.File.WriteAllBytesAsync(
                //                path,
                //                cleanBytes
                //            );

                //            aiImageUrl =
                //                $"/stickers/{fileName}";
                //        }
                //    }
                //    catch
                //    {
                //        // ignore AI failure
                //    }
                //}

                // =========================
                // FAIL SAFE
                // =========================
                if (
                    string.IsNullOrEmpty(aiImageUrl) &&
                    !giphyList.Any()
                )
                {
                    return Ok(new
                    {
                        aiSticker = (string?)null,
                        giphyStickers = new List<string>(),
                        aiLimitReached = aiCount >= 5
                    });
                }

                // =========================
                // SAVE AI STICKER
                // =========================
                if (!string.IsNullOrEmpty(aiImageUrl))
                {
                    var sticker = new Sticker
                    {
                        UserId = userId,
                        Prompt = prompt,
                        ImageUrl = aiImageUrl,
                        IsAiGenerated = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _db.Stickers.InsertOneAsync(sticker);
                }

                // =========================
                // RESPONSE
                // =========================
                return Ok(new
                {
                    aiSticker = aiImageUrl,
                    giphyStickers = giphyList,
                    aiLimitReached = aiCount >= 5
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        private async Task<byte[]> RemoveBackground(byte[] imageBytes)
        {
            var apiKey = _config["RemoveBg:ApiKey"];

            using var client = new HttpClient();

            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

            using var form = new MultipartFormDataContent();

            form.Add(
                new ByteArrayContent(imageBytes),
                "image_file",
                "image.png"
            );

            var response = await client.PostAsync(
                "https://api.remove.bg/v1.0/removebg",
                form
            );

            if (!response.IsSuccessStatusCode)
                return imageBytes; 

            return await response.Content.ReadAsByteArrayAsync();
        }

        private async Task<List<string>> GetGiphyStickers(string prompt)
        {
            var apiKey = _config["Giphy:ApiKey"];

            using var http = new HttpClient();

            var url =
                $"https://api.giphy.com/v1/stickers/search?api_key={apiKey}&q={Uri.EscapeDataString(prompt)}&limit=20&rating=g";

            var response = await http.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return new List<string>();

            var json = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);

            var list = new List<string>();

            foreach (var item in doc.RootElement.GetProperty("data").EnumerateArray())
            {
                var urlStr = item.GetProperty("images")
                                 .GetProperty("original")
                                 .GetProperty("url")
                                 .GetString();

                list.Add(urlStr);
            }

            return list;
        }
        //private async Task<string> GetGiphySticker(string prompt)
        //{
        //    var apiKey = _config["Giphy:ApiKey"];

        //    using var http = new HttpClient();

        //    var url = $"https://api.giphy.com/v1/stickers/search?api_key={apiKey}&q={Uri.EscapeDataString(prompt)}&limit=1";

        //    var response = await http.GetAsync(url);

        //    if (!response.IsSuccessStatusCode)
        //        return null;

        //    var json = await response.Content.ReadAsStringAsync();

        //    using var doc = JsonDocument.Parse(json);

        //    var data = doc.RootElement.GetProperty("data");

        //    if (data.GetArrayLength() == 0)
        //        return null;

        //    return data[0]
        //        .GetProperty("images")
        //        .GetProperty("original")
        //        .GetProperty("url")
        //        .GetString();
        //}

        // =====================================
        // GET USER STICKERS
        // =====================================
        [HttpGet("stickers/{userId}")]
        public async Task<IActionResult> GetUserStickers(
            string userId)
        {
            var data = await _db.Stickers
                .Find(x => x.UserId == userId)
                .SortByDescending(x => x.CreatedAt)
                .ToListAsync();

            return Ok(data);
        }

        [HttpPost("send-sticker")]
        public async Task<IActionResult> SendSticker(
    string senderId,
    string receiverId,
    string stickerUrl)
        {
            var msg = new Message
            {
                SenderId = senderId,
                ReceiverId = receiverId,

                StickerUrl = stickerUrl,
                IsSticker = true,

                EncryptedText =
                    _crypto.Encrypt("Sticker")
            };

            await _db.Messages
                .InsertOneAsync(msg);

            return Ok(msg);
        }
    }
}