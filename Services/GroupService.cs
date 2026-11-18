using ChatApp.Common;
using ChatApp.DTOs;
using ChatApp.Helpers;
using ChatApp.Hubs;
using ChatApp.Models;
using ChatApp.Repositories;

namespace ChatApp.Services
{
    public class GroupService : IGroupService
    {
        private readonly IGroupRepository _groupRepo;
        private readonly IGroupMemberRepository _memberRepo;
        private readonly IGroupMessageRepository _messageRepo;
        private readonly IUserRepository _userRepo;
        private readonly IFileStorageService _fileService;
        private readonly EncryptionHelper _crypto;

        public GroupService(
            IGroupRepository groupRepo,
            IGroupMemberRepository memberRepo,
            IGroupMessageRepository messageRepo,
            IUserRepository userRepo,
            IFileStorageService fileService,
            EncryptionHelper crypto)
        {
            _groupRepo = groupRepo;
            _memberRepo = memberRepo;
            _messageRepo = messageRepo;
            _userRepo = userRepo;
            _fileService = fileService;
            _crypto = crypto;
        }

        public async Task<GroupSummaryDto> CreateGroupAsync(string creatorId, CreateGroupDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Trim().Length < 3)
                throw new BadRequestException("Group name must be at least 3 characters long");

            var trimmedName = dto.Name.Trim();

            // Check if group with same name already created by this user
            var exists = await _groupRepo.ExistsByNameForUserAsync(trimmedName, creatorId);
            if (exists)
                throw new BadRequestException("You already have an active group with this name");

            var group = new ChatGroup
            {
                Name = trimmedName,
                Description = dto.Description?.Trim(),
                ImageUrl = dto.ImageUrl,
                CreatedBy = creatorId,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false,
                AutoDeleteDurationHours = dto.AutoDeleteDurationHours > 0 ? dto.AutoDeleteDurationHours : 0,
                AutoDeleteSetBy = dto.AutoDeleteDurationHours > 0 ? creatorId : null,
                AutoDeleteSetAt = dto.AutoDeleteDurationHours > 0 ? DateTime.UtcNow : null
            };

            await _groupRepo.CreateAsync(group);

            // Add creator as Owner
            var membersToAdd = new List<GroupMember>
            {
                new GroupMember
                {
                    GroupId = group.Id,
                    UserId = creatorId,
                    Role = GroupRole.Owner,
                    JoinedAt = DateTime.UtcNow
                }
            };

            // Add other valid members
            if (dto.MemberIds != null && dto.MemberIds.Count > 0)
            {
                var candidateIds = dto.MemberIds.Where(id => id != creatorId).Distinct().ToList();
                var validUsers = await _userRepo.GetByIdsAsync(candidateIds);

                foreach (var user in validUsers)
                {
                    membersToAdd.Add(new GroupMember
                    {
                        GroupId = group.Id,
                        UserId = user.Id,
                        Role = GroupRole.Member,
                        JoinedAt = DateTime.UtcNow
                    });
                }
            }

            await _memberRepo.AddMembersAsync(membersToAdd);

            // Create initial system message
            var creator = await _userRepo.GetByIdAsync(creatorId);
            var initialMessage = new GroupMessage
            {
                GroupId = group.Id,
                SenderId = creatorId,
                SenderName = creator?.FullName ?? "System",
                EncryptedText = _crypto.Encrypt($"Group \"{group.Name}\" was created"),
                IsSystemMessage = true,
                CreatedAt = DateTime.UtcNow
            };
            await _messageRepo.CreateAsync(initialMessage);

            return new GroupSummaryDto
            {
                Id = group.Id,
                Name = group.Name,
                Description = group.Description,
                ImageUrl = group.ImageUrl,
                CreatedBy = group.CreatedBy,
                CreatedAt = group.CreatedAt,
                CurrentUserRole = GroupRole.Owner,
                MemberCount = membersToAdd.Count,
                LastMessage = $"Group \"{group.Name}\" was created",
                LastMessageTime = initialMessage.CreatedAt,
                UnreadCount = 0,
                AutoDeleteDurationHours = group.AutoDeleteDurationHours
            };
        }

        public async Task<List<GroupSummaryDto>> GetUserGroupsAsync(string userId)
        {
            var memberships = await _memberRepo.GetMembershipsByUserIdAsync(userId);
            if (memberships.Count == 0) return new List<GroupSummaryDto>();

            var groupIds = memberships.Select(m => m.GroupId).ToList();
            var groups = await _groupRepo.GetByIdsAsync(groupIds);

            var result = new List<GroupSummaryDto>();
            foreach (var group in groups)
            {
                var userMembership = memberships.First(m => m.GroupId == group.Id);
                var memberCount = await _memberRepo.CountMembersAsync(group.Id);
                var lastMsg = await _messageRepo.GetLastMessageByGroupIdAsync(group.Id, userMembership.ClearedAt);

                string? lastMessageText = null;
                DateTime? lastMessageTime = null;

                if (lastMsg != null)
                {
                    lastMessageTime = lastMsg.CreatedAt;
                    if (lastMsg.IsSticker)
                    {
                        lastMessageText = $"{lastMsg.SenderName}: 🎭 Sticker";
                    }
                    else
                    {
                        try
                        {
                            var decrypted = _crypto.Decrypt(lastMsg.EncryptedText);
                            lastMessageText = lastMsg.IsSystemMessage ? decrypted : $"{lastMsg.SenderName}: {decrypted}";
                        }
                        catch
                        {
                            lastMessageText = "[Encrypted Message]";
                        }
                    }
                }

                result.Add(new GroupSummaryDto
                {
                    Id = group.Id,
                    Name = group.Name,
                    Description = group.Description,
                    ImageUrl = group.ImageUrl,
                    CreatedBy = group.CreatedBy,
                    CreatedAt = group.CreatedAt,
                    CurrentUserRole = userMembership.Role,
                    MemberCount = memberCount,
                    LastMessage = lastMessageText,
                    LastMessageTime = lastMessageTime,
                    UnreadCount = 0,
                    AutoDeleteDurationHours = group.AutoDeleteDurationHours
                });
            }

            return result.OrderByDescending(g => g.LastMessageTime ?? g.CreatedAt).ToList();
        }

        public async Task<GroupDetailDto> GetGroupDetailsAsync(string groupId, string currentUserId)
        {
            var membership = await _memberRepo.GetMembershipAsync(groupId, currentUserId);
            if (membership == null)
                throw new ForbiddenException("You are not a member of this group");

            var group = await _groupRepo.GetByIdAsync(groupId);
            if (group == null)
                throw new NotFoundException("Group not found or has been deleted");

            var groupMembers = await _memberRepo.GetMembersByGroupIdAsync(groupId);
            var userIds = groupMembers.Select(m => m.UserId).ToList();
            var users = await _userRepo.GetByIdsAsync(userIds);
            var userDict = users.ToDictionary(u => u.Id);

            var memberDtos = new List<GroupMemberDetailDto>();
            foreach (var m in groupMembers)
            {
                if (userDict.TryGetValue(m.UserId, out var u))
                {
                    memberDtos.Add(new GroupMemberDetailDto
                    {
                        UserId = u.Id,
                        FullName = u.FullName,
                        Email = u.Email,
                        ProfilePictureUrl = u.ProfilePictureUrl,
                        Role = m.Role,
                        IsOnline = ChatHub.IsUserOnline(u.Id),
                        JoinedAt = m.JoinedAt
                    });
                }
            }

            return new GroupDetailDto
            {
                Id = group.Id,
                Name = group.Name,
                Description = group.Description,
                ImageUrl = group.ImageUrl,
                CreatedBy = group.CreatedBy,
                CreatedAt = group.CreatedAt,
                CurrentUserRole = membership.Role,
                AutoDeleteDurationHours = group.AutoDeleteDurationHours,
                AutoDeleteSetBy = group.AutoDeleteSetBy,
                AutoDeleteSetAt = group.AutoDeleteSetAt,
                Members = memberDtos.OrderBy(m => m.Role == GroupRole.Owner ? 0 : m.Role == GroupRole.Admin ? 1 : 2).ThenBy(m => m.FullName).ToList()
            };
        }

        public async Task<GroupDetailDto> UpdateGroupAsync(string groupId, string currentUserId, UpdateGroupDto dto)
        {
            var membership = await _memberRepo.GetMembershipAsync(groupId, currentUserId);
            if (membership == null)
                throw new ForbiddenException("You are not a member of this group");

            if (membership.Role != GroupRole.Owner && membership.Role != GroupRole.Admin)
                throw new ForbiddenException("Only group Owners and Admins can update group details");

            var group = await _groupRepo.GetByIdAsync(groupId);
            if (group == null)
                throw new NotFoundException("Group not found or has been deleted");

            if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Trim().Length < 3)
                throw new BadRequestException("Group name must be at least 3 characters long");

            group.Name = dto.Name.Trim();
            group.Description = dto.Description?.Trim();
            if (!string.IsNullOrWhiteSpace(dto.ImageUrl))
            {
                group.ImageUrl = dto.ImageUrl;
            }

            await _groupRepo.UpdateAsync(group);

            return await GetGroupDetailsAsync(groupId, currentUserId);
        }

        public async Task DeleteGroupAsync(string groupId, string currentUserId)
        {
            var membership = await _memberRepo.GetMembershipAsync(groupId, currentUserId);
            if (membership == null)
                throw new ForbiddenException("You are not a member of this group");

            if (membership.Role != GroupRole.Owner)
                throw new ForbiddenException("Only the Group Owner can delete the group");

            var group = await _groupRepo.GetByIdAsync(groupId);
            if (group == null)
                throw new NotFoundException("Group not found");

            await _groupRepo.SoftDeleteAsync(groupId);
        }

        public async Task<List<GroupMemberDetailDto>> AddMembersAsync(string groupId, string currentUserId, AddGroupMembersDto dto)
        {
            var membership = await _memberRepo.GetMembershipAsync(groupId, currentUserId);
            if (membership == null)
                throw new ForbiddenException("You are not a member of this group");

            if (membership.Role != GroupRole.Owner && membership.Role != GroupRole.Admin)
                throw new ForbiddenException("Only group Owners and Admins can add members");

            var group = await _groupRepo.GetByIdAsync(groupId);
            if (group == null)
                throw new NotFoundException("Group not found");

            var existingMembers = await _memberRepo.GetMembersByGroupIdAsync(groupId);
            var existingUserIds = existingMembers.Select(m => m.UserId).ToHashSet();

            var newCandidateIds = dto.MemberIds.Distinct().Where(id => !existingUserIds.Contains(id)).ToList();
            if (newCandidateIds.Count == 0)
                throw new BadRequestException("All specified users are already members of this group");

            var validUsers = await _userRepo.GetByIdsAsync(newCandidateIds);
            if (validUsers.Count == 0)
                throw new BadRequestException("None of the specified user IDs were found");

            var membersToAdd = validUsers.Select(u => new GroupMember
            {
                GroupId = groupId,
                UserId = u.Id,
                Role = GroupRole.Member,
                JoinedAt = DateTime.UtcNow
            }).ToList();

            await _memberRepo.AddMembersAsync(membersToAdd);

            var addedNames = string.Join(", ", validUsers.Select(u => u.FullName));
            var sysMsg = new GroupMessage
            {
                GroupId = groupId,
                SenderId = currentUserId,
                SenderName = "System",
                EncryptedText = _crypto.Encrypt($"{addedNames} joined the group"),
                IsSystemMessage = true,
                CreatedAt = DateTime.UtcNow
            };
            await _messageRepo.CreateAsync(sysMsg);

            return validUsers.Select(u => new GroupMemberDetailDto
            {
                UserId = u.Id,
                FullName = u.FullName,
                Email = u.Email,
                ProfilePictureUrl = u.ProfilePictureUrl,
                Role = GroupRole.Member,
                IsOnline = ChatHub.IsUserOnline(u.Id),
                JoinedAt = DateTime.UtcNow
            }).ToList();
        }

        public async Task RemoveMemberAsync(string groupId, string currentUserId, string targetUserId)
        {
            var callerMembership = await _memberRepo.GetMembershipAsync(groupId, currentUserId);
            if (callerMembership == null)
                throw new ForbiddenException("You are not a member of this group");

            var targetMembership = await _memberRepo.GetMembershipAsync(groupId, targetUserId);
            if (targetMembership == null)
                throw new NotFoundException("Target user is not a member of this group");

            if (targetMembership.Role == GroupRole.Owner)
                throw new ForbiddenException("Cannot remove the Group Owner from the group");

            if (callerMembership.Role == GroupRole.Owner)
            {
                // Owner can remove anyone (except self via this method)
            }
            else if (callerMembership.Role == GroupRole.Admin)
            {
                // Admin can only remove regular Members
                if (targetMembership.Role == GroupRole.Admin)
                    throw new ForbiddenException("Admins cannot remove other Admins");
            }
            else
            {
                throw new ForbiddenException("Only group Owners and Admins can remove members");
            }

            await _memberRepo.RemoveMemberAsync(groupId, targetUserId);

            var targetUser = await _userRepo.GetByIdAsync(targetUserId);
            var sysMsg = new GroupMessage
            {
                GroupId = groupId,
                SenderId = currentUserId,
                SenderName = "System",
                EncryptedText = _crypto.Encrypt($"{targetUser?.FullName ?? "A member"} was removed from the group"),
                IsSystemMessage = true,
                CreatedAt = DateTime.UtcNow
            };
            await _messageRepo.CreateAsync(sysMsg);
        }

        public async Task LeaveGroupAsync(string groupId, string currentUserId)
        {
            var membership = await _memberRepo.GetMembershipAsync(groupId, currentUserId);
            if (membership == null)
                throw new BadRequestException("You are not a member of this group");

            var totalMembers = await _memberRepo.CountMembersAsync(groupId);

            if (membership.Role == GroupRole.Owner)
            {
                if (totalMembers > 1)
                {
                    throw new BadRequestException("As the Group Owner, you must transfer ownership to another member before leaving");
                }
                else
                {
                    // Sole member leaving => soft delete group
                    await _groupRepo.SoftDeleteAsync(groupId);
                    await _memberRepo.RemoveMemberAsync(groupId, currentUserId);
                    return;
                }
            }

            await _memberRepo.RemoveMemberAsync(groupId, currentUserId);

            var user = await _userRepo.GetByIdAsync(currentUserId);
            var sysMsg = new GroupMessage
            {
                GroupId = groupId,
                SenderId = currentUserId,
                SenderName = "System",
                EncryptedText = _crypto.Encrypt($"{user?.FullName ?? "A member"} left the group"),
                IsSystemMessage = true,
                CreatedAt = DateTime.UtcNow
            };
            await _messageRepo.CreateAsync(sysMsg);
        }

        public async Task<GroupMemberDetailDto> UpdateMemberRoleAsync(string groupId, string currentUserId, string targetUserId, string newRole)
        {
            if (!GroupRole.IsValid(newRole) || newRole == GroupRole.Owner)
                throw new BadRequestException("Role must be either 'Admin' or 'Member'. Use Transfer Ownership to assign Owner role.");

            var callerMembership = await _memberRepo.GetMembershipAsync(groupId, currentUserId);
            if (callerMembership == null || callerMembership.Role != GroupRole.Owner)
                throw new ForbiddenException("Only the Group Owner can promote or demote member roles");

            var targetMembership = await _memberRepo.GetMembershipAsync(groupId, targetUserId);
            if (targetMembership == null)
                throw new NotFoundException("Target user is not a member of this group");

            if (targetMembership.Role == GroupRole.Owner)
                throw new BadRequestException("Cannot change role of the Group Owner");

            await _memberRepo.UpdateRoleAsync(groupId, targetUserId, newRole);

            var user = await _userRepo.GetByIdAsync(targetUserId);
            var sysMsg = new GroupMessage
            {
                GroupId = groupId,
                SenderId = currentUserId,
                SenderName = "System",
                EncryptedText = _crypto.Encrypt($"{user?.FullName}'s role was updated to {newRole}"),
                IsSystemMessage = true,
                CreatedAt = DateTime.UtcNow
            };
            await _messageRepo.CreateAsync(sysMsg);

            return new GroupMemberDetailDto
            {
                UserId = targetUserId,
                FullName = user?.FullName ?? string.Empty,
                Email = user?.Email ?? string.Empty,
                ProfilePictureUrl = user?.ProfilePictureUrl,
                Role = newRole,
                IsOnline = user != null && ChatHub.IsUserOnline(user.Id),
                JoinedAt = targetMembership.JoinedAt
            };
        }

        public async Task TransferOwnershipAsync(string groupId, string currentUserId, string newOwnerId)
        {
            if (currentUserId == newOwnerId)
                throw new BadRequestException("You are already the owner of this group");

            var callerMembership = await _memberRepo.GetMembershipAsync(groupId, currentUserId);
            if (callerMembership == null || callerMembership.Role != GroupRole.Owner)
                throw new ForbiddenException("Only the Group Owner can transfer ownership");

            var newOwnerMembership = await _memberRepo.GetMembershipAsync(groupId, newOwnerId);
            if (newOwnerMembership == null)
                throw new NotFoundException("Target user is not an active member of this group");

            // Promote new owner
            await _memberRepo.UpdateRoleAsync(groupId, newOwnerId, GroupRole.Owner);

            // Demote previous owner to Admin
            await _memberRepo.UpdateRoleAsync(groupId, currentUserId, GroupRole.Admin);

            // Update CreatedBy on Group
            var group = await _groupRepo.GetByIdAsync(groupId);
            if (group != null)
            {
                group.CreatedBy = newOwnerId;
                await _groupRepo.UpdateAsync(group);
            }

            var newOwnerUser = await _userRepo.GetByIdAsync(newOwnerId);
            var sysMsg = new GroupMessage
            {
                GroupId = groupId,
                SenderId = currentUserId,
                SenderName = "System",
                EncryptedText = _crypto.Encrypt($"Ownership transferred to {newOwnerUser?.FullName}"),
                IsSystemMessage = true,
                CreatedAt = DateTime.UtcNow
            };
            await _messageRepo.CreateAsync(sysMsg);
        }

        public async Task<string> UploadGroupAvatarAsync(string? groupId, string currentUserId, IFormFile file)
        {
            if (!string.IsNullOrEmpty(groupId))
            {
                var membership = await _memberRepo.GetMembershipAsync(groupId, currentUserId);
                if (membership == null || (membership.Role != GroupRole.Owner && membership.Role != GroupRole.Admin))
                    throw new ForbiddenException("Only group Owners and Admins can update the group avatar");
            }

            var imageUrl = await _fileService.SaveImageAsync(file, "groups");

            if (!string.IsNullOrEmpty(groupId))
            {
                var group = await _groupRepo.GetByIdAsync(groupId);
                if (group != null)
                {
                    var oldImage = group.ImageUrl;
                    group.ImageUrl = imageUrl;
                    await _groupRepo.UpdateAsync(group);

                    if (!string.IsNullOrEmpty(oldImage) && !oldImage.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        _fileService.DeleteFile(oldImage);
                    }
                }
            }

            return imageUrl;
        }
    }
}

