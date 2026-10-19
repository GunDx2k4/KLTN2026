# NetArchTest Troubleshooting & Code Fixes

## 1. `Entities_ShouldHave_PrivateSetters`

### Lỗi [FAIL]:
```csharp
public class BadmintonSession : AggregateRoot<Guid>
{
    public int ShuttlecockUsed { get; set; } // [FAIL] Public setter
}
```

### Khắc phục [OK]:
```csharp
public class BadmintonSession : AggregateRoot<Guid>
{
    public int ShuttlecockUsed { get; private set; } // [OK] Private setter

    public void RecordShuttlecockUsage(int count)
    {
        if (count < 0) throw new DomainException("Số cầu không được âm.");
        ShuttlecockUsed += count;
    }
}
```

---

## 2. `Entities_ShouldHave_ParameterlessConstructor`

### Lỗi [FAIL]:
```csharp
public class SessionFee : Entity<Guid>
{
    public SessionFee(Guid sessionId, Guid userId, decimal amount)
    {
        Id = Guid.NewGuid();
        // ...
    }
}
```

### Khắc phục [OK]:
```csharp
public class SessionFee : Entity<Guid>
{
    protected SessionFee() { } // [OK] Parameterless constructor cho EF Core

    public SessionFee(Guid sessionId, Guid userId, decimal amount)
    {
        Id = Guid.NewGuid();
        // ...
    }
}
```

---

## 3. `CommandHandlers_ShouldBe_Sealed` & `Validators_ShouldBe_Sealed`

### Lỗi [FAIL]:
```csharp
public class BookSlotCommandHandler : ICommandHandler<BookSlotCommand, Guid>
public class BookSlotCommandValidator : AbstractValidator<BookSlotCommand>
```

### Khắc phục [OK]:
```csharp
public sealed class BookSlotCommandHandler : ICommandHandler<BookSlotCommand, Guid>
public sealed class BookSlotCommandValidator : AbstractValidator<BookSlotCommand>
```

---

## 4. `PresentationLayer_ShouldNotHaveDependencyOn_PersistenceLayer`

### Lỗi [FAIL]:
```csharp
public class ClubEndpoint : EndpointGroupBase
{
    public async Task<IResult> GetClubs(BadmintonDbContext db) // [FAIL] Inject DbContext
    {
        return Results.Ok(await db.ClubGroups.ToListAsync());
    }
}
```

### Khắc phục [OK]:
```csharp
public sealed class ClubEndpoint : EndpointGroupBase
{
    public async Task<IResult> GetClubs(ISender sender) // [OK] Chỉ inject ISender
    {
        var result = await sender.Send(new GetClubsQuery());
        return result.Match(onSuccess: data => Results.Ok(data));
    }
}
```
