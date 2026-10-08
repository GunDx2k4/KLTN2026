using MediatR;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.Core.Application.Messaging.CQRS;

public interface ICommandBase;
// Commands whose persistence operation must commit before further side effects.
public interface IManagesOwnTransaction : ICommandBase;
public interface ICommand : IRequest<Result>, ICommandBase;
public interface ICommand<TResponse> : IRequest<Result<TResponse>>, ICommandBase;
