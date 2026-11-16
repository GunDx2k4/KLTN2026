using System.Security.Cryptography;
using LegendsTeamVN.Core.Domain.Entities;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class LedgerAttachment : Entity<Guid>
{
    public const int MaximumContentLength = 5 * 1024 * 1024;
    public Guid LedgerId { get; private set; }
    public string OriginalFileName { get; private set; } = default!;
    public string ContentType { get; private set; } = default!;
    public long ContentLength { get; private set; }
    public string Sha256 { get; private set; } = default!;
    public byte[] Content { get; private set; } = [];

    protected LedgerAttachment() { }

    public static Result<LedgerAttachment> Create(GroupLedger ledger, string fileName, string contentType, byte[] content)
    {
        if (ledger.TransactionType != Enums.TransactionType.OUTFLOW || ledger.Id == Guid.Empty ||
            string.IsNullOrWhiteSpace(fileName) || fileName.Length > 255 || fileName.IndexOfAny(['/', '\\', '\r', '\n']) >= 0 ||
            fileName.Any(char.IsControl) || content.Length == 0 || content.Length > MaximumContentLength)
            return Invalid();
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var valid = contentType switch
        {
            "image/png" => extension == ".png" && content.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            "image/jpeg" => (extension is ".jpg" or ".jpeg") && content.AsSpan().StartsWith(new byte[] { 255, 216, 255 }),
            "application/pdf" => extension == ".pdf" && content.AsSpan().StartsWith("%PDF-"u8),
            _ => false
        };
        if (!valid) return Invalid();
        return Result.Success(new LedgerAttachment
        {
            Id = Guid.NewGuid(), LedgerId = ledger.Id, OriginalFileName = fileName.Trim(),
            ContentType = contentType, ContentLength = content.LongLength,
            Sha256 = Convert.ToHexString(SHA256.HashData(content)), Content = content.ToArray()
        });
    }

    public bool Matches(string? fileName, string? contentType, byte[] content) =>
        OriginalFileName == fileName?.Trim() && ContentType == contentType && ContentLength == content.LongLength &&
        Sha256 == Convert.ToHexString(SHA256.HashData(content));

    private static Result<LedgerAttachment> Invalid() => Result.Failure<LedgerAttachment>(
        Error.Validation("Ledger.InvalidAttachment", "Hóa đơn phải là JPEG, PNG hoặc PDF hợp lệ, tên tệp không chứa đường dẫn, tối đa 5 MiB."));
}
