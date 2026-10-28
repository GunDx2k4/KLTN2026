using LegendsTeamVN.BadmintonClub.Domain.Entities;

namespace LegendsTeamVN.BadmintonClub.Domain.Services;

public sealed record PersonalRotationSnapshot(BadmintonSession? Session, bool CanAccess);
