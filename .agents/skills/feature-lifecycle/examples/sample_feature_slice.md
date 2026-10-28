# Complete Feature Slice Example: LockSession (FR08 / UC08)

## 1. Domain Entity Method
In `src/LegendsTeamVN.BadmintonClub.Domain/Entities/BadmintonSession.cs`:
```csharp
public void Lock(Guid operatorUserId)
{
    if (Status != SessionStatus.OPEN)
        throw new DomainException($"Không thể khóa buổi chơi ở trạng thái '{Status}'. Chỉ buổi OPEN mới được khóa.");

    Status = SessionStatus.LOCKED;
    ModifiedBy = operatorUserId;
    ModifiedOnUtc = DateTime.UtcNow;

    AddDomainEvent(new SessionLockedDomainEvent(Id, GroupId));
}
```

## 2. Command, Validator & Handler
In `src/LegendsTeamVN.BadmintonClub.Application/Features/Sessions/Commands/LockSession/LockSessionCommandHandler.cs`:
```csharp
namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Commands.LockSession;

public sealed record LockSessionCommand(Guid GroupId, Guid SessionId) : ICommand<Guid>;

public sealed class LockSessionCommandValidator : AbstractValidator<LockSessionCommand>
{
    public LockSessionCommandValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty().WithMessage("Mã nhóm không được để trống.");
        RuleFor(x => x.SessionId).NotEmpty().WithMessage("Mã buổi sinh hoạt không được để trống.");
    }
}

public sealed class LockSessionCommandHandler(
    ISessionRepository sessionRepository,
    ICurrentUserService currentUserService
) : ICommandHandler<LockSessionCommand, Guid>
{
    public async Task<Result<Guid>> Handle(LockSessionCommand request, CancellationToken cancellationToken)
    {
        var operatorId = currentUserService.UserId;
        if (!operatorId.HasValue)
            return Result.Failure<Guid>(Error.Unauthorized("Auth.Unauthorized", "Yêu cầu đăng nhập."));

        var session = await sessionRepository.FindByIdAsync(request.SessionId, cancellationToken);
        if (session is null || session.GroupId != request.GroupId)
            return Result.Failure<Guid>(Error.NotFound("Session.NotFound", "Không tìm thấy buổi sinh hoạt."));

        session.Lock(operatorId.Value);

        await sessionRepository.SaveChangesAsync(cancellationToken);
        return Result.Success(session.Id);
    }
}
```

## 3. Minimal API Endpoint
In `src/LegendsTeamVN.BadmintonClub.Presentation/Endpoints/Session/LockSessionEndpoint.cs`:
```csharp
namespace LegendsTeamVN.BadmintonClub.Presentation.Endpoints.Session;

public sealed class LockSessionEndpoint : EndpointGroupBase
{
    public override void Map(RouteGroupBuilder group)
    {
        group.MapPost("{groupId:guid}/sessions/{sessionId:guid}/lock", LockSession)
            .WithName("LockSession")
            .WithSummary("Khóa buổi sinh hoạt (ngừng nhận đăng ký slot)")
            .RequireGroupPermission(GroupPermissions.Operations.SessionUpdate);
    }

    private static async Task<IResult> LockSession(
        Guid groupId,
        Guid sessionId,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new LockSessionCommand(groupId, sessionId), cancellationToken);
        return result.Match(
            onSuccess: id => Results.Ok(new { SessionId = id }),
            onFailure: error => Results.BadRequest(error));
    }
}
```
