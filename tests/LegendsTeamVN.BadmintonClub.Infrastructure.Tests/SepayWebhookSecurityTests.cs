using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Infrastructure.Payments;
using Microsoft.Extensions.Options;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Tests;

internal sealed class ReconciliationTestOptions<T>(T value) : IOptionsMonitor<T> where T : class
{
    public T CurrentValue => value;
    public T Get(string? name) => value;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
}

public sealed class SepayWebhookSecurityTests
{
    private const long Timestamp = 1793772000;
    private const string Secret = "uc18-independent-test-vector";
    private const string Body = """{"id":92704,"gateway":"Vietcombank","transactionDate":"2026-11-04 13:00:00","accountNumber":"00123456789","subAccount":"","transferType":"in","transferAmount":50000,"referenceCode":null,"content":"CLB ABCDEF123456 012345ABCDEF"}""";
    private const string Signature = "sha256=7596a20bc54388c9fceade088e3574c8a868249a0775ce5a59872992489e4d9f";
    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeSeconds(Timestamp);
    }

    private static (SepayWebhookVerifier Verifier, ReconciliationOptions Options, Guid Bank) Create()
    {
        var bank = Guid.NewGuid();
        var options = new ReconciliationOptions { CompanyId = "uc18-vector", Banks = { [bank] = new() { Secret = Secret, Gateway = "Vietcombank" } } };
        return (new(new ReconciliationTestOptions<ReconciliationOptions>(options), new Clock()), options, bank);
    }

    private static string Sign(byte[] body, long timestamp) => "sha256=" + Convert.ToHexString(HMACSHA256.HashData(
        Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(timestamp.ToString(CultureInfo.InvariantCulture) + ".").Concat(body).ToArray())).ToLowerInvariant();

    [Fact]
    public void IndependentLocalVectorVerifiesExactBytesAndVietnamTimeBecomesUtc()
    {
        var (verifier, _, bank) = Create();
        var result = verifier.Verify(bank, Encoding.UTF8.GetBytes(Body), Signature, Timestamp.ToString(CultureInfo.InvariantCulture));
        Assert.True(result.IsSuccess, result.Error.Message);
        Assert.Equal("00123456789", result.Value.Evidence.AccountNumber);
        Assert.Equal(new DateTime(2026, 11, 4, 6, 0, 0, DateTimeKind.Utc), result.Value.Evidence.TransactionTime);
        Assert.Null(result.Value.Evidence.ReferenceCode);
    }

    [Theory]
    [InlineData(-301)]
    [InlineData(301)]
    public void FreshSignatureCannotBypassTimestampWindow(int offset)
    {
        var (verifier, _, bank) = Create();
        var bytes = Encoding.UTF8.GetBytes(Body);
        Assert.Equal("Reconciliation.InvalidSignature", verifier.Verify(bank, bytes, Sign(bytes, Timestamp + offset),
            (Timestamp + offset).ToString(CultureInfo.InvariantCulture)).Error.Code);
    }

    [Fact]
    public void ReserializedOrModifiedBodyDoesNotMatchOriginalSignature()
    {
        var (verifier, _, bank) = Create();
        Assert.True(verifier.Verify(bank, Encoding.UTF8.GetBytes(Body + " "), Signature, Timestamp.ToString(CultureInfo.InvariantCulture)).IsFailure);
        Assert.True(verifier.Verify(bank, Encoding.UTF8.GetBytes(Body.Replace("50000", "50001")), Signature, Timestamp.ToString(CultureInfo.InvariantCulture)).IsFailure);
        Assert.True(verifier.Verify(bank, Encoding.UTF8.GetBytes(Body), "sha256=" + new string('x', 64), Timestamp.ToString(CultureInfo.InvariantCulture)).IsFailure);
    }

    [Fact]
    public void RevokedOrUnknownBankCredentialCannotVerify()
    {
        var (verifier, options, bank) = Create();
        Assert.True(verifier.Verify(Guid.NewGuid(), Encoding.UTF8.GetBytes(Body), Signature, Timestamp.ToString(CultureInfo.InvariantCulture)).IsFailure);
        options.Banks[bank].Secret = "";
        Assert.True(verifier.Verify(bank, Encoding.UTF8.GetBytes(Body), Signature, Timestamp.ToString(CultureInfo.InvariantCulture)).IsFailure);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"id\":1,\"id\":2}")]
    [InlineData("{")]
    public void SignedMalformedOrAmbiguousJsonIsRejected(string body)
    {
        var (verifier, _, bank) = Create();
        var bytes = Encoding.UTF8.GetBytes(body);
        Assert.Equal("Reconciliation.InvalidPayload", verifier.Verify(bank, bytes, Sign(bytes, Timestamp),
            Timestamp.ToString(CultureInfo.InvariantCulture)).Error.Code);
    }

    [Fact]
    public void ProviderTransactionTimeIsIndependentOfSigningTimestamp()
    {
        var (verifier, _, bank) = Create();
        var bytes = Encoding.UTF8.GetBytes(Body.Replace("2026-11-04 13:00:00", "2024-01-01 00:00:00"));
        Assert.True(verifier.Verify(bank, bytes, Sign(bytes, Timestamp), Timestamp.ToString(CultureInfo.InvariantCulture)).IsSuccess);
    }
}
