using System.Text;
using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Infrastructure.Payments;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Tests;

public sealed class WebhookEvidenceArchiveTests
{
    [Fact]
    public async Task MissingArchiveConfigurationCannotSilentlyLoseVerifiedEvidence()
    {
        var archive = new WebhookEvidenceArchive(new ReconciliationTestOptions<ReconciliationOptions>(new()), TimeProvider.System);
        await Assert.ThrowsAsync<InvalidOperationException>(() => archive.ArchiveAsync(Guid.NewGuid(), 123, Encoding.UTF8.GetBytes("{}"), default));
    }

    [Fact]
    public async Task EvidenceIsStoredOutsideApplicationWithOwnerOnlyPermissions()
    {
        var directory = Path.Combine(Path.GetTempPath(), "uc18-archive-test-" + Guid.NewGuid().ToString("N"));
        var archive = new WebhookEvidenceArchive(new ReconciliationTestOptions<ReconciliationOptions>(new() { EvidenceArchiveDirectory = directory }), TimeProvider.System);
        var bytes = Encoding.UTF8.GetBytes("{\"id\":123}");
        try
        {
            await archive.ArchiveAsync(Guid.NewGuid(), 123, bytes, default);
            var file = Assert.Single(Directory.GetFiles(directory));
            using var json = JsonDocument.Parse(await File.ReadAllBytesAsync(file));
            Assert.Equal(bytes, Convert.FromBase64String(json.RootElement.GetProperty("RawBody").GetString()!));
            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(directory));
            }
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }
}
