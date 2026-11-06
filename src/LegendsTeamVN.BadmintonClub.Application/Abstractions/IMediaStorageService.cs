namespace LegendsTeamVN.BadmintonClub.Application.Abstractions;

public interface IMediaStorageService
{
    Task<string> UploadImageAsync(Stream fileStream, string fileName, string? folder = "badminton_app", CancellationToken cancellationToken = default);
}

