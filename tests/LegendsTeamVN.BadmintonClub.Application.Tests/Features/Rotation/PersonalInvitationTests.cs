using System.Text;
using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Events;
using LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Personal;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Events;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Rotation;

public sealed class PersonalInvitationTests
{
    private static MatchInvitationRoster Roster() => new(Guid.NewGuid(), Guid.NewGuid(), 2, DateTime.UtcNow,
        [new(Guid.NewGuid(), "A one", SideTeam.TEAM_A), new(Guid.NewGuid(), "A two", SideTeam.TEAM_A),
         new(Guid.NewGuid(), "B one", SideTeam.TEAM_B), new(Guid.NewGuid(), "B two", SideTeam.TEAM_B)]);
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void EachPlayerGetsTheirOwnPartnerAndOtherTeam(int index)
    {
        var roster = Roster(); var self = roster.Players[index];
        var change = new RotationChangedDomainEvent(roster.SessionId, 7, roster.TimeStart!.Value, roster.MatchId, roster.CourtNumber, roster.Players.Select(p => p.UserId).ToArray());
        var dto = MatchInvitationMapper.Map(change, roster, self.UserId);
        Assert.Equal(roster.Players.Single(p => p.Team == self.Team && p.UserId != self.UserId).UserId, dto.Teammate.UserId);
        Assert.Equal(roster.Players.Where(p => p.Team != self.Team).Select(p => p.UserId), dto.Opponents.Select(p => p.UserId));
        Assert.Equal(change.Id, dto.EventId); Assert.Equal(7, dto.RotationVersion);
        Assert.DoesNotContain(self.Name, MatchInvitationMapper.Body(dto));
    }

    [Fact]
    public void LongUnicodeNamesAreBoundedWithoutSplittingRunesOrExposingOtherFields()
    {
        var roster = Roster();
        roster = roster with { Players = roster.Players.Select(p => p with { Name = string.Concat(Enumerable.Repeat(char.ConvertFromUtf32(0x20000), 200)) }).ToArray() };
        var change = new RotationChangedDomainEvent(roster.SessionId, 1, DateTime.UtcNow, roster.MatchId, 2, []);
        var dto = MatchInvitationMapper.Map(change, roster, roster.Players[0].UserId);
        Assert.Equal(40, dto.Teammate.Name.EnumerateRunes().Count());
        Assert.True(Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(dto, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping })) < 2500);
        var wire = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var data = new Dictionary<string, string>
        {
            ["eventId"] = change.Id.ToString(), ["sessionId"] = roster.SessionId.ToString(), ["matchId"] = roster.MatchId.ToString(),
            ["courtNumber"] = "2", ["rotationVersion"] = "9223372036854775807", ["timeStart"] = DateTime.UtcNow.ToString("O"),
            ["teammate"] = JsonSerializer.Serialize(dto.Teammate, wire), ["opponents"] = JsonSerializer.Serialize(dto.Opponents, wire)
        };
        var payload = JsonSerializer.Serialize(new { notification = new { title = "Đến lượt vào sân", body = MatchInvitationMapper.Body(dto) }, data });
        Assert.True(Encoding.UTF8.GetByteCount(payload) < 4096);
    }

    [Fact]
    public void InvalidDoublesRosterFailsClosed()
    {
        var roster = Roster(); roster = roster with { Players = roster.Players.Take(3).ToArray() };
        Assert.Throws<InvalidOperationException>(() => MatchInvitationPolicy.ForPlayer(roster, roster.Players[0].UserId));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task StartKeepsFourNotificationsNineLegacyEnvelopesAndOnlyAddsPersonalContent(bool personal)
    {
        var roster = Roster(); var outbox = Substitute.For<IOutboxRepository>(); var notifications = Substitute.For<INotificationRepository>();
        var sessions = Substitute.For<ISessionRepository>();
        sessions.GetMatchInvitationRosterAsync(roster.SessionId, roster.MatchId, false, default).Returns(roster);
        var flags = Substitute.For<IOptionsMonitor<SessionRotationOptions>>(); flags.CurrentValue.Returns(new SessionRotationOptions { PersonalScheduleEnabled = personal });
        var saved = new List<Notification>(); var envelopes = new List<OutboxMessage>();
        notifications.When(n => n.Add(Arg.Any<Notification>())).Do(c => saved.Add(c.Arg<Notification>()));
        outbox.When(o => o.Add(Arg.Any<OutboxMessage>())).Do(c => envelopes.Add(c.Arg<OutboxMessage>()));
        var change = new RotationChangedDomainEvent(roster.SessionId, 4, DateTime.UtcNow, roster.MatchId, 2, roster.Players.Select(p => p.UserId).ToArray());
        await new RotationChangedDomainEventHandler(outbox, notifications, sessions, flags).Handle(change, default);
        Assert.Equal(4, saved.Count); Assert.Equal(9, envelopes.Count);
        Assert.Equal(3, envelopes.Select(e => e.Kind).Distinct().Count());
        Assert.All(envelopes, e => Assert.Equal(change.Id, JsonSerializer.Deserialize<RotationChangedDomainEvent>(e.Payload)!.Id));
        Assert.All(saved, n => Assert.Equal(personal, n.Content.Contains("Bạn cặp:")));
        if (personal) foreach (var n in saved) Assert.Equal(MatchInvitationMapper.Body(MatchInvitationMapper.Map(change, roster, n.UserId)), n.Content);
    }
}
