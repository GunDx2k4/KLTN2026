using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Personal;
using LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Personal.Get;
using LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Personal.Pause;
using LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Personal.Resume;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Identity.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Rotation;

public sealed class PersonalScheduleTests
{
    private readonly ISessionRepository sessions = Substitute.For<ISessionRepository>();
    private readonly ICurrentUserService user = Substitute.For<ICurrentUserService>();
    private readonly IOptionsMonitor<SessionRotationOptions> options = Substitute.For<IOptionsMonitor<SessionRotationOptions>>();
    private readonly SessionRotationOptions flags = new() { Enabled = true, PersonalScheduleEnabled = true, SelfPauseEnabled = true };
    private readonly BadmintonSession session;
    private readonly Guid actor = Guid.NewGuid();
    private readonly PersonalScheduleService service;
    public PersonalScheduleTests()
    {
        var now = DateTime.UtcNow;
        session = new(Guid.NewGuid(), "Personal", now.AddHours(-1), now.AddHours(1), "Test", courtCount: 1);
        for (var i = 0; i < 5; i++)
        {
            var b = new SessionBooking(session.Id, i == 0 ? actor : Guid.NewGuid()); b.MarkPresent(actor, now);
            typeof(SessionBooking).GetProperty(nameof(b.User))!.SetValue(b, new AppUser(b.UserId, "Name", "private@example.test", "private-phone"));
            session.Bookings.Add(b);
        }
        session.CompleteAttendance(actor, now); session.InitializeRotation(actor, now); session.ClearDomainEvents();
        user.UserId.Returns(actor); options.CurrentValue.Returns(flags);
        sessions.GetPersonalRotationAsync(session.Id, actor, Arg.Any<bool>()).Returns(new PersonalRotationSnapshot(session, true));
        service = new(sessions, user, TimeProvider.System, options);
    }

    [Fact]
    public async Task GetIsReadOnlyAndNeverReturnsHostOrPrivateFields()
    {
        var v = session.RotationVersion;
        var r = await service.GetAsync(session.Id, default); Assert.True(r.IsSuccess);
        var json = System.Text.Json.JsonSerializer.Serialize(r.Value);
        Assert.DoesNotContain("private", json); Assert.DoesNotContain("OriginalLineup", json);
        Assert.Equal(v, session.RotationVersion); Assert.Empty(session.DomainEvents);
        Assert.DoesNotContain(sessions.ReceivedCalls(), c => c.GetMethodInfo().Name is "SaveChangesAsync" or "AddRotationMatches" or "GetRotationAsync");
        Assert.Null(r.Value!.EstimatedStartAt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeniedParticipantCannotBypassUsingEnabledFlag(bool enabled)
    {
        flags.PersonalScheduleEnabled = enabled;
        sessions.GetPersonalRotationAsync(session.Id, actor, false).Returns(new PersonalRotationSnapshot(session, false));
        Assert.Equal("PersonalSchedule.Forbidden", (await service.GetAsync(session.Id, default)).Error.Code);
        Assert.Empty(session.DomainEvents);
    }

    [Fact]
    public async Task MissingAndUnauthorizedHaveDistinctErrors()
    {
        user.UserId.Returns((Guid?)null);
        Assert.Equal("User.Unauthorized", (await service.GetAsync(session.Id, default)).Error.Code);
        user.UserId.Returns(actor);
        sessions.GetPersonalRotationAsync(session.Id, actor, false).Returns(new PersonalRotationSnapshot(null, false));
        Assert.Equal("Session.NotFound", (await service.GetAsync(session.Id, default)).Error.Code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IntegrityFailuresAreControlledConflicts(bool pauseMetadata)
    {
        if (pauseMetadata) sessions.GetPersonalRotationAsync(session.Id, actor, false).ThrowsAsync(new InvalidRotationPauseMetadataException(session.Id, actor));
        else sessions.GetPersonalRotationAsync(session.Id, actor, false).ThrowsAsync(new InvalidLineupSnapshotException(session.Id));
        Assert.Equal(pauseMetadata ? "Rotation.InvalidPauseMetadata" : "Rotation.InvalidSnapshot", (await service.GetAsync(session.Id, default)).Error.Code);
    }

    [Fact]
    public async Task FeatureFlagsBlockNewPauseButAllowRecoveryResume()
    {
        var v = session.RotationVersion;
        Assert.True((await service.SetPauseAsync(session.Id, true, v, default)).IsSuccess);
        flags.Enabled = false; flags.SelfPauseEnabled = false;
        Assert.Equal("Rotation.SelfPauseDisabled", (await service.SetPauseAsync(session.Id, true, session.RotationVersion, default)).Error.Code);
        Assert.True((await service.SetPauseAsync(session.Id, false, session.RotationVersion, default)).IsSuccess);
        flags.PersonalScheduleEnabled = false;
        Assert.Equal("PersonalSchedule.Disabled", (await service.GetAsync(session.Id, default)).Error.Code);
        Assert.Equal("PersonalSchedule.Disabled", (await service.SetPauseAsync(session.Id, false, session.RotationVersion, default)).Error.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1L)]
    public void ValidatorsRejectMissingOrNegativeVersion(long? version)
    {
        Assert.False(new PauseMyMatchesCommandValidator().Validate(new PauseMyMatchesCommand(session.Id, version)).IsValid);
        Assert.False(new ResumeMyMatchesCommandValidator().Validate(new ResumeMyMatchesCommand(session.Id, version)).IsValid);
        Assert.False(new GetMyMatchesQueryValidator().Validate(new GetMyMatchesQuery(Guid.Empty)).IsValid);
    }
}
