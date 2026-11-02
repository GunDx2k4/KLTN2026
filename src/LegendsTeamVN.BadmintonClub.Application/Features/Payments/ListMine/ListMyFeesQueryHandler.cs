using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.Extensions.Options;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.ListMine;

public sealed class ListMyFeesQueryHandler(IPaymentRepository fees, ICurrentUserService currentUser, IOptionsMonitor<PaymentsOptions> options)
    : IQueryHandler<ListMyFeesQuery, PersonalFeeListResponse>
{
    public async Task<Result<PersonalFeeListResponse>> Handle(ListMyFeesQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid user) return Result.Failure<PersonalFeeListResponse>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        var result = await fees.ListMineAsync(user, request.Page, request.PageSize, request.GroupId, request.SessionId, request.PaymentStatus, ct);
        var flags = options.CurrentValue;
        return Result.Success(new PersonalFeeListResponse(result.Fees.Select(f => PersonalFeeMapper.Map(f, flags.Enabled, flags.WritesEnabled)).ToArray(),
            result.Total, request.Page, request.PageSize));
    }
}
