using System.Security.Claims;

using HiringPlatform.Application.Common;
using HiringPlatform.Application.Identity;
using HiringPlatform.Domain.Common;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace HiringPlatform.Api;

public static class ApiSupport
{
    public static UserId UserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? new UserId(id)
            : throw new UnauthorizedAccessException("Missing user id claim");

    public static Task SignInUser(this HttpContext http, UserDto user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return http.SignInAsync(new ClaimsPrincipal(identity));
    }

    public static Task SignOutUser(this HttpContext http) => http.SignOutAsync();

    public static IResult ToHttp<T>(this Result<T> result) => result.IsSuccess
        ? Results.Ok(result.Value)
        : result.Error!.Kind switch
        {
            ErrorKind.Validation => Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [result.Error.Message] }),
            ErrorKind.NotFound => Results.NotFound(new { error = result.Error.Message }),
            ErrorKind.Forbidden => Results.Json(new { error = result.Error.Message }, statusCode: StatusCodes.Status403Forbidden),
            ErrorKind.Conflict => Results.Conflict(new { error = result.Error.Message }),
            ErrorKind.Unauthorized => Results.Unauthorized(),
            _ => Results.BadRequest(new { error = result.Error.Message }),
        };
}
