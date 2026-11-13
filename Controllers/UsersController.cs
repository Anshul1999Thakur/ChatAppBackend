using ChatApp.Common;
using ChatApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChatApp.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            var currentUserId = User.GetUserId();
            var result = await _userService.GetUserDirectoryAsync(currentUserId);
            return Ok(result);
        }

        // Backward compatibility overload for existing frontend calling /api/users/{currentUserId}
        [HttpGet("{currentUserId}")]
        public async Task<IActionResult> GetUsersForId(string currentUserId)
        {
            var authenticatedId = User.GetUserId();

            // Validate caller is requesting their own contact directory
            if (!string.Equals(authenticatedId, currentUserId, StringComparison.OrdinalIgnoreCase))
            {
                throw new ForbiddenException("Cannot access user directory on behalf of another user");
            }

            var result = await _userService.GetUserDirectoryAsync(authenticatedId);
            return Ok(result);
        }
    }
}
