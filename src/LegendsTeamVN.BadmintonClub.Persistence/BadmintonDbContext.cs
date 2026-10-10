using LegendsTeamVN.BadmintonClub.Domain.Entities;
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
    public DbSet<GroupLedger> GroupLedgers => Set<GroupLedger>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BadmintonDbContext).Assembly);
    }
}
