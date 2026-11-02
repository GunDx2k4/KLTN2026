using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.Extensions.Options;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.GetMine;

public sealed class GetMyFeeQueryHandler(IPaymentRepository fees, ICurrentUserService currentUser, IOptionsMonitor<PaymentsOptions> options)
    : IQueryHandler<GetMyFeeQuery, PersonalFeeResponse>
{
    public async Task<Result<PersonalFeeResponse>> Handle(GetMyFeeQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid user) return Result.Failure<PersonalFeeResponse>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        var fee = await fees.GetMineAsync(request.FeeId, user, false, ct);
        if (fee is null) return Result.Failure<PersonalFeeResponse>(Error.NotFound("Payments.NotFound", "Không tìm thấy khoản thu."));
        var flags = options.CurrentValue;
        return Result.Success(PersonalFeeMapper.Map(fee, flags.Enabled, flags.WritesEnabled));
    }
}
