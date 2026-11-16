using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Exceptions;
using Npgsql;
using LegendsTeamVN.Core.Persistence.DbContexts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Persistence;

public class BadmintonDbContext(DbContextOptions<BadmintonDbContext> options, IPublisher publisher) : DbContextUnitOfWork<BadmintonDbContext>(options, publisher)
{
    // 22 Entities from ERD
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<UserDevice> UserDevices => Set<UserDevice>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<MemberReview> MemberReviews => Set<MemberReview>();
    public DbSet<GroupReview> GroupReviews => Set<GroupReview>();
    public DbSet<ClubGroup> ClubGroups => Set<ClubGroup>();
    public DbSet<GroupPermission> GroupPermissions => Set<GroupPermission>();
    public DbSet<GroupRole> GroupRoles => Set<GroupRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<GroupSchedule> GroupSchedules => Set<GroupSchedule>();
    public DbSet<GroupBankAccount> GroupBankAccounts => Set<GroupBankAccount>();
    public DbSet<GroupFeeRule> GroupFeeRules => Set<GroupFeeRule>();
    public DbSet<JoinRequest> JoinRequests => Set<JoinRequest>();
    public DbSet<BadmintonSession> BadmintonSessions => Set<BadmintonSession>();
    public DbSet<SessionBooking> SessionBookings => Set<SessionBooking>();
    public DbSet<GuestRecruitment> GuestRecruitments => Set<GuestRecruitment>();
    public DbSet<SessionMatch> SessionMatches => Set<SessionMatch>();
    public DbSet<MatchPlayer> MatchPlayers => Set<MatchPlayer>();
    public DbSet<SessionFee> SessionFees => Set<SessionFee>();
    public DbSet<BankTransaction> BankTransactions => Set<BankTransaction>();
    public DbSet<MonthlyContribution> MonthlyContributions => Set<MonthlyContribution>();
    public DbSet<LedgerAttachment> LedgerAttachments => Set<LedgerAttachment>();
    public DbSet<GroupLedger> GroupLedgers => Set<GroupLedger>();


    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ReputationWriteGuard.Apply(ChangeTracker);
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
            (pg.SqlState == PostgresErrorCodes.UniqueViolation && pg.ConstraintName == "IX_group_review_id_session_id_user" ||
             pg.SqlState is "55P03" or "40P01" or "40001" &&
             (ChangeTracker.Entries<BadmintonSession>().Any(e => e.Property(x => x.ReputationVersion).IsModified) ||
              ChangeTracker.Entries<GroupReview>().Any(e => e.State == EntityState.Added && e.Entity.SessionId.HasValue))))
        {
            throw new ReputationConflictException(ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.LockNotAvailable or PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure })
        {
            throw new LedgerWriteConflictException("Ledger.Busy", "Sổ quỹ đang được cập nhật, hãy thử lại với cùng mã yêu cầu.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_group_ledger_id_group_request_id" or "IX_group_ledger_monthly_contribution_id" or "IX_monthly_contribution_id_group_user_id_contribution_month" })
        {
            throw new LedgerWriteConflictException("Ledger.Busy", "Sổ quỹ đang được cập nhật, hãy thử lại với cùng mã yêu cầu.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_bank_transaction_provider_transaction_id" or "ux_bank_transaction_company_provider" or
                "IX_group_ledger_fee_id" or "IX_group_ledger_bank_transaction_id" })
        {
            throw new ReconciliationWriteConflictException(ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_session_fee_payment_code" } &&
            ChangeTracker.Entries<SessionFee>().Any(e => e.State == EntityState.Added && e.Entity.IsPublished))
        {
            throw new SettlementPublicationConflictException("Settlement.PaymentCodeConflict", "Mã thanh toán bị trùng, chưa phát hành khoản thu.", ex);
        }
    }
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ReputationWriteGuard.Apply(ChangeTracker);
        try
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
        catch (DbUpdateConcurrencyException ex) when (IsReputationEntityConflict(ex))
        {
            throw new ReputationConflictException(ex);
        }
    }
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ReputationWriteGuard.Apply(ChangeTracker);
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex) when (IsReputationEntityConflict(ex))
        {
            throw new ReputationConflictException(ex);
        }
    }

    private static bool IsReputationEntityConflict(DbUpdateConcurrencyException exception) =>
        exception.Entries.Count > 0 && exception.Entries.All(entry =>
            entry.Entity is AppUser or ClubGroup or BadmintonSession or SessionBooking);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BadmintonDbContext).Assembly);
    }
}
