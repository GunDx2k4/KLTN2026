using Microsoft.Extensions.Options;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Common;

internal sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T> where T : class
{
    public T CurrentValue => value;
    public T Get(string? name) => value;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
