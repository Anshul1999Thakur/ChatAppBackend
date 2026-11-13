using System.Net;

namespace ChatApp.Common
{
    public class AppException : Exception
    {
        public HttpStatusCode StatusCode { get; }
        public List<string>? Errors { get; }

        public AppException(string message, HttpStatusCode statusCode = HttpStatusCode.BadRequest, List<string>? errors = null)
            : base(message)
        {
            StatusCode = statusCode;
            Errors = errors;
        }
    }

    public class BadRequestException : AppException
    {
        public BadRequestException(string message, List<string>? errors = null)
            : base(message, HttpStatusCode.BadRequest, errors) { }
    }

    public class NotFoundException : AppException
    {
        public NotFoundException(string message)
            : base(message, HttpStatusCode.NotFound) { }
    }

    public class UnauthorizedException : AppException
    {
        public UnauthorizedException(string message = "Unauthorized access")
            : base(message, HttpStatusCode.Unauthorized) { }
    }

    public class ForbiddenException : AppException
    {
        public ForbiddenException(string message = "You do not have permission to perform this action")
            : base(message, HttpStatusCode.Forbidden) { }
    }
}

