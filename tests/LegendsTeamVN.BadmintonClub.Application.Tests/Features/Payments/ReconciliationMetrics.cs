using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Payments;

internal sealed class ReconciliationMetrics : DbTransactionInterceptor, ILoggerProvider
{
    public bool RejectCommit { get; set; }

    public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
        InterceptionResult result, CancellationToken cancellationToken = default)
    {
        if (RejectCommit) throw new InvalidOperationException("UC18 injected commit failure");
        return ValueTask.FromResult(result);
    }

    public ConcurrentQueue<KeyValuePair<string, double>> Values { get; } = new();

    public ILogger CreateLogger(string categoryName) => new MetricsLogger(this);
    public void Dispose() { }

    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Values.Enqueue(new("CommitMs", eventData.Duration.TotalMilliseconds));
        return Task.CompletedTask;
    }

    private sealed class MetricsLogger(ReconciliationMetrics metrics) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (state is not IEnumerable<KeyValuePair<string, object?>> fields) return;
            foreach (var field in fields)
                if (field.Key is "AuthMs" or "TotalMs" or "LockWaitMs" or "TransactionMs" && field.Value is double value)
                    metrics.Values.Enqueue(new(field.Key, value));
        }
    }
}
