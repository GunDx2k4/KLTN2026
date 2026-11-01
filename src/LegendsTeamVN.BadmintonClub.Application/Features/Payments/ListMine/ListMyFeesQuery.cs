using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.ListMine;

public sealed record ListMyFeesQuery(int Page = 1, int PageSize = 20, Guid? GroupId = null, Guid? SessionId = null,
    PaymentStatus? PaymentStatus = null) : IQuery<PersonalFeeListResponse>;
