using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using QRCoder;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Payments;

public sealed class VietQrImageRenderer : IVietQrImageRenderer
{
    public byte[] RenderPng(string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        using var data = QRCodeGenerator.GenerateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        using var renderer = new PngByteQRCode(data);
        return renderer.GetGraphic(8);
    }
}
