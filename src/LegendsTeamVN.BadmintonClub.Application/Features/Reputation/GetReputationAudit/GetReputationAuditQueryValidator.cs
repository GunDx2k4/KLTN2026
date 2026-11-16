using FluentValidation;
namespace LegendsTeamVN.BadmintonClub.Application.Features.Reputation.GetReputationAudit;
public sealed class GetReputationAuditQueryValidator : AbstractValidator<GetReputationAuditQuery>
{
    public GetReputationAuditQueryValidator()
    {
        RuleFor(x => x.ClubId).NotEmpty(); RuleFor(x => x.SessionId).NotEmpty(); RuleFor(x => x.Page).InclusiveBetween(1, 1000000); RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
