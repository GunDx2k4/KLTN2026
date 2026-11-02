namespace LegendsTeamVN.BadmintonClub.Application.Abstractions;

public interface IVietQrImageRenderer
{
    byte[] RenderPng(string payload);
}
