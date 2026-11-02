using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.Extensions.Options;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Services;

public sealed class SettlementPreviewTokenService(IOptions<SettlementOptions> options) : ISettlementPreviewTokenService
{
    private static readonly JsonSerializerOptions CanonicalJson = new()
    {
        Converters = { new CanonicalDecimalConverter(), new CanonicalDateTimeConverter() }
    };

    public SettlementPreviewToken Create(Guid actorId, Guid groupId, Guid sessionId, SettlementCosts costs,
        IReadOnlyCollection<SettlementAdjustment> adjustments, bool confirmRemainingNoShows,
        string sourceHash, SettlementAllocation allocation, DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc) throw new ArgumentException("Thời gian phải sử dụng UTC.", nameof(utcNow));
        var identity = new SettlementPreviewIdentity(1, SettlementOptions.CurrentPolicyVersion, Guid.NewGuid(), actorId,
            groupId, sessionId, utcNow, utcNow.AddMinutes(15), HashRequest(costs, adjustments, confirmRemainingNoShows),
            sourceHash, HashResult(allocation));
        var payload = Encode(JsonSerializer.SerializeToUtf8Bytes(identity, CanonicalJson));
        var signature = HMACSHA256.HashData(SigningKey(), Encoding.UTF8.GetBytes(payload));
        return new SettlementPreviewToken($"{payload}.{Encode(signature)}", identity);
    }

    public Result<SettlementPreviewIdentity> Validate(string token, Guid actorId, Guid groupId, Guid sessionId,
        SettlementCosts costs, IReadOnlyCollection<SettlementAdjustment> adjustments, bool confirmRemainingNoShows)
    {
        var key = SigningKey();
        if (string.IsNullOrWhiteSpace(token) || token.Length > 8192) return Invalid();
        var parts = token.Split('.');
        if (parts.Length != 2) return Invalid();
        try
        {
            var signature = Decode(parts[1]);
            var expected = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(parts[0]));
            if (!CryptographicOperations.FixedTimeEquals(expected, signature)) return Invalid();
            var identity = JsonSerializer.Deserialize<SettlementPreviewIdentity>(Decode(parts[0]), CanonicalJson);
            if (identity is null || identity.Version != 1 || identity.PolicyVersion != SettlementOptions.CurrentPolicyVersion ||
                identity.PreviewId == Guid.Empty || identity.IssuedAt.Kind != DateTimeKind.Utc ||
                identity.ExpiresAt.Kind != DateTimeKind.Utc || identity.ExpiresAt != identity.IssuedAt.AddMinutes(15) ||
                !IsHash(identity.SourceHash) || !IsHash(identity.ResultHash) || !IsHash(identity.RequestHash)) return Invalid();
            if (identity.ActorId != actorId || identity.GroupId != groupId || identity.SessionId != sessionId)
                return Result.Failure<SettlementPreviewIdentity>(Error.Forbidden("Settlement.PreviewActorMismatch", "Bản xem trước không thuộc người duyệt, câu lạc bộ hoặc buổi chơi này."));
            if (identity.RequestHash != HashRequest(costs, adjustments, confirmRemainingNoShows)) return Invalid();
            return Result.Success(identity);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return Invalid();
        }
    }

    public string HashSource(BadmintonSession session, GroupFeeRule rule, IReadOnlyCollection<AppUser> users,
        IReadOnlyCollection<GroupMember> memberships) => Hash(new
    {
        PolicyVersion = SettlementOptions.CurrentPolicyVersion,
        Session = new { session.Id, session.GroupId, session.StartTime, session.EndTime, session.Status, session.IsDeleted },
        Bookings = session.Bookings.OrderBy(b => b.Id.ToString("N"), StringComparer.Ordinal)
            .Select(b => new { b.Id, b.SessionId, b.UserId, b.Status, b.IsDeleted, b.CheckinAt, b.AttendanceMarkedAt, b.AttendanceMarkedBy }),
        Matches = session.Matches.OrderBy(m => m.Id.ToString("N"), StringComparer.Ordinal)
            .Select(m => new { m.Id, m.Status, m.IsDeleted }),
        Rule = new { rule.Id, rule.GroupId, rule.FeeMode, rule.MonthlyFee, rule.DailyMemberFee, rule.GuestFee,
            rule.FemaleDiscountMonthly, rule.FemaleDiscountDaily, rule.FemaleDiscountGuest, rule.RoundRule, rule.AppliedAt },
        Users = users.OrderBy(u => u.Id.ToString("N"), StringComparer.Ordinal).Select(u => new { u.Id, u.Gender, u.IsDeleted }),
        Memberships = memberships.OrderBy(m => m.Id.ToString("N"), StringComparer.Ordinal)
            .Select(m => new { m.Id, m.GroupId, m.UserId, m.MemberType, m.MonthlyExpiryDate, m.Status, m.IsDeleted })
    });

    public string HashResult(SettlementAllocation allocation) => Hash(allocation with
    {
        Members = allocation.Members.OrderBy(m => m.UserId.ToString("N"), StringComparer.Ordinal).ToArray()
    });

    private static string HashRequest(SettlementCosts costs, IReadOnlyCollection<SettlementAdjustment> adjustments,
        bool confirmRemainingNoShows) => Hash(new
    {
        PolicyVersion = SettlementOptions.CurrentPolicyVersion,
        Costs = costs,
        Adjustments = adjustments.OrderBy(a => a.UserId.ToString("N"), StringComparer.Ordinal)
            .Select(a => new { a.UserId, a.FinalAmount, Reason = a.Reason?.Trim() }),
        ConfirmRemainingNoShows = confirmRemainingNoShows
    });

    private byte[] SigningKey()
    {
        var key = Encoding.UTF8.GetBytes(options.Value.PreviewSigningKey);
        if (key.Length < 32) throw new InvalidOperationException("Settlement:PreviewSigningKey phải được cấp riêng qua môi trường và có ít nhất 32 byte.");
        return key;
    }

    private static bool IsHash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, CanonicalJson)));
    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] Decode(string value) => Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/').PadRight((value.Length + 3) / 4 * 4, '='));
    private static Result<SettlementPreviewIdentity> Invalid() => Result.Failure<SettlementPreviewIdentity>(Error.Validation("Settlement.InvalidPreviewToken", "Bản xem trước không hợp lệ hoặc dữ liệu đã bị thay đổi."));

    private sealed class CanonicalDecimalConverter : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetDecimal();
        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
            writer.WriteRawValue(value.ToString("G29", CultureInfo.InvariantCulture));
    }

    private sealed class CanonicalDateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetDateTime();
        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("Thời gian nguồn quyết toán phải sử dụng UTC.");
            writer.WriteStringValue(value.ToString("O", CultureInfo.InvariantCulture));
        }
    }
}
