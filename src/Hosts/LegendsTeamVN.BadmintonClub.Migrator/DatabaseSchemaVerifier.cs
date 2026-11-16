using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Migrator;

public static class DatabaseSchemaVerifier
{
    public static async Task VerifyAsync(DbContext context, CancellationToken cancellationToken = default)
    {
        if (!context.Database.GetMigrations().Any())
            throw new InvalidOperationException($"Không tìm thấy migrations cho {context.GetType().Name}.");
        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
        if (pending.Length != 0)
            throw new InvalidOperationException($"Schema {context.GetType().Name} chưa sẵn sàng: {string.Join(", ", pending)}. Cần review SQL, backup và áp migration trước khi deploy API.");
    }
}
