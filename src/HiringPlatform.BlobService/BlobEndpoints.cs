using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace HiringPlatform.BlobService;

internal static partial class BlobEndpoints
{
    private const long DefaultMaximumBytes = 100 * 1024 * 1024;

    public static RouteGroupBuilder MapBlobEndpoints(this RouteGroupBuilder api)
    {
        var blobs = api.MapGroup("/blobs").RequireAuthorization();
        blobs.MapPost("", Upload);
        blobs.MapMethods("/{id:guid}", ["HEAD"], Head);
        blobs.MapGet("/{id:guid}", Download);
        blobs.MapDelete("/{id:guid}", Delete);
        return api;
    }

    private static async Task<IResult> Upload(
        HttpRequest request,
        ClaimsPrincipal principal,
        BlobStorage storage,
        IConfiguration configuration,
        CancellationToken ct)
    {
        var length = request.ContentLength;
        var maximum = configuration.GetValue("BlobStorage:MaximumBytes", DefaultMaximumBytes);
        if (length is null or <= 0)
            return Validation("Content-Length must be greater than zero");
        if (length > maximum)
            return Results.Json(new
            {
                error = $"Blob must be no larger than {maximum} bytes"
            }, statusCode: 413);

        var name = SafeName(request.Headers["X-Blob-Name"].ToString());
        if (name is null)
            return Validation("X-Blob-Name is required and must be no longer than 240 characters");
        var purpose = request.Headers["X-Blob-Purpose"].ToString().Trim().ToLowerInvariant();
        if (!PurposePattern().IsMatch(purpose))
            return Validation("X-Blob-Purpose must contain 1-64 lowercase letters, digits, dots, underscores, or hyphens");

        var contentType = request.ContentType?.Split(';', 2)[0].Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(contentType))
            contentType = "application/octet-stream";

        var id = Guid.CreateVersion7();
        await storage.Put(principal.UserId(), id, name, contentType, purpose, request.Body, length.Value, ct);
        return Results.Created($"/api/blobs/{id}",
            new BlobDescriptor(id, name, contentType, length.Value, purpose, $"/api/blobs/{id}"));
    }

    private static async Task<IResult> Head(
        Guid id,
        ClaimsPrincipal principal,
        BlobStorage storage,
        HttpResponse response,
        CancellationToken ct)
    {
        if (await storage.Stat(principal.UserId(), id, ct) is not { } blob)
            return Results.NotFound();
        ApplyHeaders(response, blob, inline: true);
        response.ContentLength = blob.Size;
        return Results.Empty;
    }

    private static async Task Download(
        Guid id,
        bool download,
        ClaimsPrincipal principal,
        BlobStorage storage,
        HttpRequest request,
        HttpResponse response,
        CancellationToken ct)
    {
        if (await storage.Stat(principal.UserId(), id, ct) is not { } blob)
        {
            response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        ApplyHeaders(response, blob, inline: !download);
        if (!TryRange(request.Headers.Range, blob.Size, out var offset, out var length))
        {
            response.ContentLength = blob.Size;
            await storage.CopyTo(principal.UserId(), id, response.Body, null, null, ct);
            return;
        }

        response.StatusCode = StatusCodes.Status206PartialContent;
        response.ContentLength = length;
        response.Headers.ContentRange = $"bytes {offset}-{offset + length - 1}/{blob.Size}";
        await storage.CopyTo(principal.UserId(), id, response.Body, offset, length, ct);
    }

    private static async Task<IResult> Delete(
        Guid id,
        ClaimsPrincipal principal,
        BlobStorage storage,
        CancellationToken ct)
    {
        if (await storage.Stat(principal.UserId(), id, ct) is null)
            return Results.NotFound();
        await storage.Remove(principal.UserId(), id, ct);
        return Results.NoContent();
    }

    private static void ApplyHeaders(HttpResponse response, StoredBlob blob, bool inline)
    {
        response.ContentType = blob.ContentType;
        response.Headers.AcceptRanges = "bytes";
        response.Headers.XContentTypeOptions = "nosniff";
        response.Headers["X-Blob-Purpose"] = blob.Purpose;
        var disposition = new ContentDispositionHeaderValue(inline && IsSafeInline(blob.ContentType) ? "inline" : "attachment")
        {
            FileNameStar = blob.Name,
        };
        response.Headers.ContentDisposition = disposition.ToString();
    }

    private static bool TryRange(string? value, long total, out long offset, out long length)
    {
        offset = 0;
        length = total;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var match = RangePattern().Match(value);
        if (!match.Success || !long.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out offset))
            return false;
        var end = total - 1;
        if (match.Groups[2].Success && long.TryParse(match.Groups[2].Value, CultureInfo.InvariantCulture, out var requestedEnd))
            end = Math.Min(end, requestedEnd);
        if (offset < 0 || offset > end)
        {
            offset = 0;
            length = total;
            return false;
        }
        length = end - offset + 1;
        return true;
    }

    private static string? SafeName(string value)
    {
        var name = Path.GetFileName(value.Trim());
        return name.Length is > 0 and <= 240 ? name : null;
    }

    private static bool IsSafeInline(string contentType) =>
        contentType == "application/pdf" || contentType.StartsWith("image/", StringComparison.Ordinal) ||
        contentType.StartsWith("audio/", StringComparison.Ordinal) || contentType.StartsWith("video/", StringComparison.Ordinal);

    private static IResult Validation(string message) => Results.ValidationProblem(
        new Dictionary<string, string[]> { ["request"] = [message] });

    private static Guid UserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new UnauthorizedAccessException("Missing user id claim");

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9._-]{0,63})$")]
    private static partial Regex PurposePattern();

    [GeneratedRegex("^bytes=(\\d+)-(\\d*)$", RegexOptions.IgnoreCase)]
    private static partial Regex RangePattern();
}
