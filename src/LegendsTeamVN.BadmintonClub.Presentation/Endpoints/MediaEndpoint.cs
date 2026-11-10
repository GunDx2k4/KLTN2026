using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.Core.Presentation.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace LegendsTeamVN.BadmintonClub.Presentation.Endpoints;

public class MediaEndpoint : EndpointGroupBase
{
    protected override string Name => "media";
    protected override string? Tag => "Media";

    protected override void Map(RouteGroupBuilder group)
    {
        group.MapPost("upload", UploadImage)
             .WithName("UploadImage")
             .WithSummary("Tải ảnh lên Cloudinary và lấy URL an toàn (HTTPS)")
             .WithDescription("Nhận file ảnh multipart/form-data (jpg, jpeg, png, webp, tối đa 5MB) và upload lên Cloudinary.")
             .DisableAntiforgery();
    }

    private static async Task<IResult> UploadImage(
        IFormFile? file,
        [FromForm] string? folder,
        IMediaStorageService storageService,
        CancellationToken ct)
    {
        if (file == null || file.Length == 0)
        {
            return Results.BadRequest(Result.Failure(Error.Validation("Media.EmptyFile", "Vui lòng chọn tệp ảnh để tải lên.")));
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            return Results.BadRequest(Result.Failure(Error.Validation("Media.FileTooLarge", "Dung lượng ảnh tối đa là 5MB.")));
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext))
        {
            return Results.BadRequest(Result.Failure(Error.Validation("Media.InvalidFormat", "Chỉ hỗ trợ tệp ảnh định dạng JPG, JPEG, PNG, WEBP hoặc GIF.")));
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var secureUrl = await storageService.UploadImageAsync(stream, file.FileName, folder ?? "badminton_app", ct);

            return Results.Ok(new UploadImageResponse(secureUrl));
        }
        catch (Exception ex)
        {
            return Results.BadRequest(Result.Failure(Error.Failure("Media.UploadError", ex.Message)));
        }
    }
}

public record UploadImageResponse(string Url);

