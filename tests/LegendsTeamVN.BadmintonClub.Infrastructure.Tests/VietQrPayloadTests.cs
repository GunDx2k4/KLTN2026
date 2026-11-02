using System.Globalization;
using System.Text;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Infrastructure.Payments;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Tests;

public sealed class VietQrPayloadTests
{
    private const string Code = "CLB 0123456789AB FEDCBA987654";
    private static PaymentBankSnapshot Bank => new(Guid.NewGuid(), "970436", "TEST", "00123456789", "TEST");

    [Theory]
    [InlineData("vi-VN")]
    [InlineData("en-US")]
    public void PatchedAnperoPreservesEveryFieldAndPassesIndependentTlvAndCrc(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var result = new VietQrPayloadGenerator().Generate(Bank, 50000, Code);
            Assert.True(result.IsSuccess);
            var raw = result.Value;
            var fields = Decode(raw);
            var merchant = Decode(fields["38"]);
            var bank = Decode(merchant["01"]);
            Assert.Equal("A000000727", merchant["00"]);
            Assert.Equal("QRIBFTTA", merchant["02"]);
            Assert.Equal("970436", bank["00"]);
            Assert.Equal("00123456789", bank["01"]);
            Assert.Equal("50000", fields["54"]);
            Assert.Equal("704", fields["53"]);
            Assert.Equal("VN", fields["58"]);
            Assert.Equal(Code, Decode(fields["62"])["08"]);
            Assert.Equal(29, Decode(fields["62"])["08"].Length);
            Assert.Equal(IndependentCrc(raw[..^4]), raw[^4..]);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.50")]
    [InlineData("10000000000000")]
    public void UnsupportedAmountsAreRejectedWithoutRounding(string amount)
    {
        var result = new VietQrPayloadGenerator().Generate(Bank, decimal.Parse(amount, CultureInfo.InvariantCulture), Code);
        Assert.True(result.IsFailure);
        Assert.Equal("Payments.InvalidQrInput", result.Error.Code);
    }

    [Fact]
    public void MalformedBankAndNonCanonicalCodeAreRejected()
    {
        var generator = new VietQrPayloadGenerator();
        Assert.True(generator.Generate(Bank with { BankBin = "123" }, 50000, Code).IsFailure);
        Assert.True(generator.Generate(Bank with { AccountNumber = "001A" }, 50000, Code).IsFailure);
        Assert.True(generator.Generate(Bank, 50000, Code + " ").IsFailure);
        Assert.True(generator.Generate(Bank, 50000, Code.ToLowerInvariant()).IsFailure);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("9999999999999")]
    public void SupportedIntegerBoundariesRemainExact(string value)
    {
        var result = new VietQrPayloadGenerator().Generate(Bank, decimal.Parse(value, CultureInfo.InvariantCulture), Code);
        Assert.True(result.IsSuccess);
        Assert.Equal(value, Decode(result.Value)["54"]);
    }

    private static Dictionary<string, string> Decode(string raw)
    {
        var result = new Dictionary<string, string>();
        for (var offset = 0; offset < raw.Length;)
        {
            Assert.True(offset + 4 <= raw.Length);
            var tag = raw.Substring(offset, 2);
            var length = int.Parse(raw.AsSpan(offset + 2, 2), CultureInfo.InvariantCulture);
            Assert.True(offset + 4 + length <= raw.Length);
            result.Add(tag, raw.Substring(offset + 4, length));
            offset += 4 + length;
        }
        return result;
    }

    private static string IndependentCrc(string text)
    {
        ushort crc = 0xffff;
        foreach (var value in Encoding.UTF8.GetBytes(text))
        {
            crc ^= (ushort)(value << 8);
            for (var bit = 0; bit < 8; bit++) crc = (ushort)((crc & 0x8000) != 0 ? (crc << 1) ^ 0x1021 : crc << 1);
        }
        return crc.ToString("X4", CultureInfo.InvariantCulture);
    }
}
