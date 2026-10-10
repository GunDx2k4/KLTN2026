using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Domain.Aggregates;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class AppUser : AggregateRoot<Guid>
{
    public string? PhoneNumber { get; private set; }
    public string? EmailAddress { get; private set; }
    public string? PasswordHash { get; private set; }
    public string FullName { get; private set; } = default!;
    public short Gender { get; private set; }
    public string? AvatarUrl { get; private set; }
    /// <summary>
    /// Trình độ tự đánh giá của người dùng. Miền giá trị: 1=NB, 2=Y, 3=TBY, 4=TB, 5=TBK, 6=K.
    /// </summary>
    public SkillLevel SkillLevel { get; private set; } = SkillLevel.NB;
    public decimal? LastLat { get; private set; }
    public decimal? LastLng { get; private set; }
    public bool IsOpenForGuest { get; private set; }
    public decimal ReputationScore { get; private set; }
    public bool IsActive { get; private set; }

    public virtual ICollection<UserDevice> Devices { get; private set; } = new List<UserDevice>();
    public virtual ICollection<Notification> Notifications { get; private set; } = new List<Notification>();
    public virtual ICollection<MemberReview> ReviewsGiven { get; private set; } = new List<MemberReview>();
    public virtual ICollection<MemberReview> ReviewsReceived { get; private set; } = new List<MemberReview>();
    public virtual ICollection<GroupReview> GroupReviews { get; private set; } = new List<GroupReview>();
    public virtual ICollection<GroupMember> GroupMemberships { get; private set; } = new List<GroupMember>();
    public virtual ICollection<JoinRequest> JoinRequests { get; private set; } = new List<JoinRequest>();
    public virtual ICollection<SessionBooking> SessionBookings { get; private set; } = new List<SessionBooking>();
    public virtual ICollection<MatchPlayer> MatchPlayers { get; private set; } = new List<MatchPlayer>();
    public virtual ICollection<SessionFee> SessionFees { get; private set; } = new List<SessionFee>();
    public virtual ICollection<GroupLedger> LedgerTransactions { get; private set; } = new List<GroupLedger>();

    protected AppUser() { }

    public AppUser(
        Guid id,
        string fullName,
        string? emailAddress = null,
        string? phoneNumber = null,
        string? passwordHash = null,
        short gender = 0,
        string? avatarUrl = null,
        SkillLevel skillLevel = SkillLevel.NB,
        decimal? lastLat = null,
        decimal? lastLng = null,
        bool isOpenForGuest = true,
        decimal reputationScore = 100.00m,
        bool isActive = true)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        FullName = fullName;
        EmailAddress = emailAddress;
        PhoneNumber = phoneNumber;
        PasswordHash = passwordHash;
        Gender = gender;
        AvatarUrl = avatarUrl;
        SkillLevel = skillLevel;
        LastLat = lastLat;
        LastLng = lastLng;
        IsOpenForGuest = isOpenForGuest;
        ReputationScore = reputationScore;
        IsActive = isActive;
    }

    public static AppUser CreateStandard(
        Guid id,
        string fullName,
        string? emailAddress,
        string? phoneNumber,
        short gender,
        SkillLevel skillLevel)
    {
        return new AppUser(
            id: id,
            fullName: fullName,
            emailAddress: emailAddress,
            phoneNumber: phoneNumber,
            gender: gender,
            skillLevel: skillLevel,
            isOpenForGuest: true,
            reputationScore: 100.00m,
            isActive: true
        );
    }

    public static AppUser CreateExternal(
        Guid id,
        string fullName,
        string emailAddress,
        string? avatarUrl = null)
    {
        return new AppUser(
            id: id,
            fullName: fullName,
            emailAddress: emailAddress,
            avatarUrl: avatarUrl,
            skillLevel: SkillLevel.NB,
            isOpenForGuest: true,
            reputationScore: 100.00m,
            isActive: true
        );
    }

    public void UpdateProfile(string fullName, short gender, string? avatarUrl, SkillLevel skillLevel, bool isOpenForGuest)
    {
        FullName = fullName;
        Gender = gender;
        AvatarUrl = avatarUrl;
        SkillLevel = skillLevel;
        IsOpenForGuest = isOpenForGuest;
    }

    public void UpdateLocation(decimal? lat, decimal? lng)
    {
        LastLat = lat;
        LastLng = lng;
    }

    public void UpdateReputation(decimal score)
    {
        ReputationScore = score;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }
}
