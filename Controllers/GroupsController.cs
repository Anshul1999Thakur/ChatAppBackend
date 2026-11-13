using ChatApp.Common;
using ChatApp.DTOs;
using ChatApp.Hubs;
using ChatApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace ChatApp.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/groups")]
    public class GroupsController : ControllerBase
    {
        private readonly IGroupService _groupService;
        private readonly IGroupMessageService _groupMessageService;
        private readonly IHubContext<ChatHub> _hubContext;

        public GroupsController(
            IGroupService groupService,
            IGroupMessageService groupMessageService,
            IHubContext<ChatHub> hubContext)
        {
            _groupService = groupService;
            _groupMessageService = groupMessageService;
            _hubContext = hubContext;
        }

        // ==========================================
        // CREATE GROUP
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> CreateGroup([FromBody] CreateGroupDto dto)
        {
            var currentUserId = User.GetUserId();
            var createdGroup = await _groupService.CreateGroupAsync(currentUserId, dto);

            // Notify initial members via SignalR
            if (dto.MemberIds != null)
            {
                foreach (var memberId in dto.MemberIds)
                {
                    var conns = ChatHub.GetUserConnections(memberId);
                    if (conns.Count > 0)
                    {
                        await _hubContext.Clients.Clients(conns).SendAsync("GroupCreated", createdGroup);
                    }
                }
            }

            return StatusCode(StatusCodes.Status201Created, createdGroup);
        }

        // ==========================================
        // READ USER'S GROUPS
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> GetUserGroups()
        {
            var currentUserId = User.GetUserId();
            var groups = await _groupService.GetUserGroupsAsync(currentUserId);
            return Ok(groups);
        }

        // ==========================================
        // GET GROUP DETAILS & MEMBERS
        // ==========================================
        [HttpGet("{groupId}")]
        public async Task<IActionResult> GetGroupDetails(string groupId)
        {
            var currentUserId = User.GetUserId();
            var details = await _groupService.GetGroupDetailsAsync(groupId, currentUserId);
            return Ok(details);
        }

        // ==========================================
        // UPDATE GROUP
        // ==========================================
        [HttpPut("{groupId}")]
        public async Task<IActionResult> UpdateGroup(string groupId, [FromBody] UpdateGroupDto dto)
        {
            var currentUserId = User.GetUserId();
            var updated = await _groupService.UpdateGroupAsync(groupId, currentUserId, dto);

            // Broadcast real-time update to group
            await _hubContext.Clients.Group($"group_{groupId}").SendAsync("GroupUpdated", updated);

            return Ok(updated);
        }

        // ==========================================
        // DELETE GROUP (Soft Delete - Owner Only)
        // ==========================================
        [HttpDelete("{groupId}")]
        public async Task<IActionResult> DeleteGroup(string groupId)
        {
            var currentUserId = User.GetUserId();
            await _groupService.DeleteGroupAsync(groupId, currentUserId);

            // Broadcast real-time deletion
            await _hubContext.Clients.Group($"group_{groupId}").SendAsync("GroupDeleted", groupId);

            return Ok(ApiResponse.Ok("Group deleted successfully"));
        }

        // ==========================================
        // ADD MEMBERS
        // ==========================================
        [HttpPost("{groupId}/members")]
        public async Task<IActionResult> AddMembers(string groupId, [FromBody] AddGroupMembersDto dto)
        {
            var currentUserId = User.GetUserId();
            var addedMembers = await _groupService.AddMembersAsync(groupId, currentUserId, dto);

            // Broadcast to group members
            foreach (var member in addedMembers)
            {
                await _hubContext.Clients.Group($"group_{groupId}").SendAsync("GroupMemberAdded", new { groupId, member });
                await _hubContext.Clients.Group($"group_{groupId}").SendAsync("GroupMemberUpdated", new { groupId, member, action = "Added" });

                // Also notify the newly added user directly
                var conns = ChatHub.GetUserConnections(member.UserId);
                if (conns.Count > 0)
                {
                    var groupDetails = await _groupService.GetGroupDetailsAsync(groupId, currentUserId);
                    await _hubContext.Clients.Clients(conns).SendAsync("GroupCreated", groupDetails);
                }
            }

            return Ok(addedMembers);
        }

        // ==========================================
        // REMOVE MEMBER
        // ==========================================
        [HttpDelete("{groupId}/members/{userId}")]
        public async Task<IActionResult> RemoveMember(string groupId, string userId)
        {
            var currentUserId = User.GetUserId();
            await _groupService.RemoveMemberAsync(groupId, currentUserId, userId);

            // Broadcast to group
            await _hubContext.Clients.Group($"group_{groupId}").SendAsync("GroupMemberRemoved", new { groupId, userId });
            await _hubContext.Clients.Group($"group_{groupId}").SendAsync("GroupMemberUpdated", new { groupId, userId, action = "Removed" });

            // Notify removed user
            var conns = ChatHub.GetUserConnections(userId);
            if (conns.Count > 0)
            {
                await _hubContext.Clients.Clients(conns).SendAsync("GroupDeleted", groupId);
            }

            return Ok(ApiResponse.Ok("Member removed successfully"));
        }

        // ==========================================
        // LEAVE GROUP
        // ==========================================
        [HttpPost("{groupId}/leave")]
        public async Task<IActionResult> LeaveGroup(string groupId)
        {
            var currentUserId = User.GetUserId();
            await _groupService.LeaveGroupAsync(groupId, currentUserId);

            // Broadcast to group
            await _hubContext.Clients.Group($"group_{groupId}").SendAsync("GroupMemberLeft", new { groupId, userId = currentUserId });
            await _hubContext.Clients.Group($"group_{groupId}").SendAsync("GroupMemberUpdated", new { groupId, userId = currentUserId, action = "Left" });

            return Ok(ApiResponse.Ok("You left the group successfully"));
        }

        // ==========================================
        // UPDATE MEMBER ROLE (Promote/Demote)
        // ==========================================
        [HttpPut("{groupId}/members/{userId}/role")]
        public async Task<IActionResult> UpdateRole(string groupId, string userId, [FromBody] UpdateMemberRoleDto dto)
        {
            var currentUserId = User.GetUserId();
            var updated = await _groupService.UpdateMemberRoleAsync(groupId, currentUserId, userId, dto.Role);

            // Broadcast to group
            await _hubContext.Clients.Group($"group_{groupId}").SendAsync("GroupRoleUpdated", new { groupId, userId, newRole = dto.Role });
            await _hubContext.Clients.Group($"group_{groupId}").SendAsync("GroupMemberUpdated", new { groupId, member = updated, action = "RoleChanged" });

            return Ok(updated);
        }

        // ==========================================
        // TRANSFER OWNERSHIP
        // ==========================================
        [HttpPost("{groupId}/transfer-ownership")]
        public async Task<IActionResult> TransferOwnership(string groupId, [FromBody] TransferOwnershipDto dto)
        {
            var currentUserId = User.GetUserId();
            await _groupService.TransferOwnershipAsync(groupId, currentUserId, dto.NewOwnerId);

            // Broadcast to group
            await _hubContext.Clients.Group($"group_{groupId}").SendAsync("GroupOwnershipTransferred", new
            {
                groupId,
                previousOwnerId = currentUserId,
                newOwnerId = dto.NewOwnerId
            });
            await _hubContext.Clients.Group($"group_{groupId}").SendAsync("GroupMemberUpdated", new { groupId, newOwnerId = dto.NewOwnerId, action = "OwnershipTransferred" });

            return Ok(ApiResponse.Ok("Ownership transferred successfully"));
        }

        // ==========================================
        // UPLOAD GROUP AVATAR
        // ==========================================
        [HttpPost("upload-avatar")]
        [HttpPost("{groupId}/upload-avatar")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadAvatar(string? groupId, IFormFile file)
        {
            var currentUserId = User.GetUserId();
            var imageUrl = await _groupService.UploadGroupAvatarAsync(groupId, currentUserId, file);

            if (!string.IsNullOrEmpty(groupId))
            {
                await _hubContext.Clients.Group($"group_{groupId}").SendAsync("GroupUpdated", new { groupId, imageUrl });
            }

            return Ok(new { imageUrl });
        }

        // ==========================================
        // GROUP MESSAGES HISTORY
        // ==========================================
        [HttpGet("{groupId}/messages")]
        public async Task<IActionResult> GetMessages(
            string groupId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 30)
        {
            var currentUserId = User.GetUserId();
            var messages = await _groupMessageService.GetGroupMessagesAsync(groupId, currentUserId, page, pageSize);
            return Ok(messages);
        }

        // ==========================================
        // SEND GROUP MESSAGE (REST Fallback)
        // ==========================================
        [HttpPost("{groupId}/messages")]
        public async Task<IActionResult> SendMessage(string groupId, [FromBody] SendGroupMessageDto dto)
        {
            var currentUserId = User.GetUserId();
            var message = await _groupMessageService.SendGroupMessageAsync(groupId, currentUserId, dto.Text);

            // Broadcast to real-time group
            await _hubContext.Clients.Group($"group_{groupId}").SendAsync("ReceiveGroupMessage", message);
            await _hubContext.Clients.Group($"group_{groupId}").SendAsync("GroupMessageReceived", message);

            return Ok(message);
        }
    }
}

