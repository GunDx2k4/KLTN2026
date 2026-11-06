using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Services;
namespace LegendsTeamVN.BadmintonClub.Application.Abstractions;
public sealed record ReputationAssessmentSource(BadmintonSession Session, ClubGroup Group,
    IReadOnlyList<SessionFee> Fees, IReadOnlyList<GroupLedger> Ledger, IReadOnlyList<BankTransaction> Banks,
    QualitySessionEvidence Quality);
