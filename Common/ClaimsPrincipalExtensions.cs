using System.Security.Claims;

namespace ChatApp.Common
{
    public static class ClaimsPrincipalExtensions
    {
        public static string GetUserId(this ClaimsPrincipal? principal)
        {
            if (principal == null)
                throw new UnauthorizedException("User is not authenticated");

            var userId = principal.FindFirst("UserId")?.Value
                ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? principal.FindFirst("nameid")?.Value
                ?? principal.FindFirst("sub")?.Value;

            if (string.IsNullOrWhiteSpace(userId))
                throw new UnauthorizedException("User ID claim is missing from authentication token");

            return userId;
        }

        public static string? TryGetUserId(this ClaimsPrincipal? principal)
        {
            if (principal == null) return null;

            return principal.FindFirst("UserId")?.Value
                ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? principal.FindFirst("nameid")?.Value
                ?? principal.FindFirst("sub")?.Value;
        }

        public static string GetFullName(this ClaimsPrincipal? principal)
        {
            return principal?.FindFirst("FullName")?.Value
                ?? principal?.FindFirst(ClaimTypes.Name)?.Value
                ?? "User";
        }

        public static string GetEmail(this ClaimsPrincipal? principal)
        {
            return principal?.FindFirst(ClaimTypes.Email)?.Value
                ?? principal?.FindFirst("email")?.Value
                ?? string.Empty;
        }
    }
}

