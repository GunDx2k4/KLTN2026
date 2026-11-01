using System.Buffers.Binary;
using LegendsTeamVN.BadmintonClub.Infrastructure.Payments;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Tests;

public sealed class VietQrImageRendererTests
{
    [Fact]
    public void RendererProducesSquarePngWithoutSystemDrawing()
    {
        var png = new VietQrImageRenderer().RenderPng("UC17 renderer test; not a bank payload");
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png.Take(8));
        var width = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4));
        Assert.Equal(width, height);
        Assert.True(width > 100);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void EmptyPayloadIsRejected(string payload) =>
        Assert.Throws<ArgumentException>(() => new VietQrImageRenderer().RenderPng(payload));
}
