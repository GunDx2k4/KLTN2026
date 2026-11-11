using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using Microsoft.Extensions.Options;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Payments;

public sealed class WebhookEvidenceArchive(IOptionsMonitor<ReconciliationOptions> options, TimeProvider clock) : IWebhookEvidenceArchive
{
    public async Task ArchiveAsync(Guid bankAccountId, long transactionId, byte[] body, CancellationToken ct)
    {
        var directory = options.CurrentValue.EvidenceArchiveDirectory;
        if (!Path.IsPathFullyQualified(directory)) throw new InvalidOperationException("Cần thư mục cô lập bằng chứng riêng.");
        directory = Path.GetFullPath(directory);
        if (directory.StartsWith(Path.GetFullPath(AppContext.BaseDirectory), StringComparison.Ordinal))
            throw new InvalidOperationException("Không lưu bằng chứng trong thư mục ứng dụng.");
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(directory);
        else Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        if (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(directory) &
            (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
             UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
            throw new InvalidOperationException("Thư mục bằng chứng phải giới hạn quyền cho tài khoản ứng dụng.");
        var fileOptions = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None,
            Options = FileOptions.Asynchronous | FileOptions.WriteThrough };
        if (!OperatingSystem.IsWindows()) fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        await using var file = new FileStream(Path.Combine(directory, Guid.NewGuid().ToString("N") + ".json"), fileOptions);
        await JsonSerializer.SerializeAsync(file, new { BankAccountId = bankAccountId, ProviderTransactionId = transactionId,
            VerifiedAt = clock.GetUtcNow(), RawBody = Convert.ToBase64String(body) }, cancellationToken: ct);
        await file.FlushAsync(ct);
        file.Flush(flushToDisk: true);
    }
}
