using MediatR;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.Core.Application.Messaging.CQRS;

public interface ICommandBase;
public interface ICommand : IRequest<Result>, ICommandBase;
public interface ICommand<TResponse> : IRequest<Result<TResponse>>, ICommandBase;
