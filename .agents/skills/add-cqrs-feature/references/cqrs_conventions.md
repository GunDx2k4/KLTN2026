# CQRS Conventions & Code Standards (LegendsTeamVN)

## 1. Class Conventions
- **Command / Query Records:** `public sealed record ...Command(...) : ICommand<TResult>;`
- **Validators:** `public sealed class ...Validator : AbstractValidator<...Command>`
  - Must inherit `AbstractValidator<T>`
  - Must have suffix `Validator`
  - Must be `sealed`
- **Handlers:** `public sealed class ...Handler(...) : ICommandHandler<...Command, TResult>`
  - Must implement `ICommandHandler<TCommand, TResult>` or `IQueryHandler<TQuery, TResult>`
  - Must have suffix `CommandHandler` or `QueryHandler`
  - Must be `sealed`
  - Must use Primary Constructor syntax for dependency injection
- **Endpoints:** `public sealed class ...Endpoint : EndpointGroupBase`
  - Must inherit `EndpointGroupBase`
  - Must have suffix `Endpoint`
  - Must inject only `ISender`
  - Must protect club routes with `.RequireGroupPermission(GroupPermissions.*)`
  - Must use `result.Match(onSuccess: data => Results.Ok(data))`

## 2. Handler 6-Step Orchestration Flow
1. Check User Context (`currentUserService.UserId`).
2. Load Aggregate Root via Repository.
3. Check Entity existence (return `Result.Failure(Error.NotFound(...))` if null).
4. Invoke rich domain method on the Entity (`entity.ExecuteAction(...)`).
5. Persist changes via `repository.SaveChangesAsync()`.
6. Return `Result.Success(...)`.
