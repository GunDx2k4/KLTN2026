using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.Core.Application.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using LegendsTeamVN.BadmintonClub.Application.Features.Attendance;
using LegendsTeamVN.BadmintonClub.Application.Features.Rotation;

namespace LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMessageHandler(Assembly.GetExecutingAssembly());
        var transactionIndex = services.ToList().FindIndex(descriptor =>
            descriptor.ServiceType == typeof(MediatR.IPipelineBehavior<,>) &&
            descriptor.ImplementationType == typeof(LegendsTeamVN.Core.Application.Behaviors.UnitOfWorkBehavior<,>));
        services.Insert(transactionIndex, ServiceDescriptor.Transient(typeof(MediatR.IPipelineBehavior<Features.Sessions.Settlement.ApproveSettlementCommand, LegendsTeamVN.Core.Utilities.Results.Result<DTOs.Sessions.Responses.SessionSettlementResponse>>),
            typeof(Features.Sessions.Settlement.SettlementLockBehavior)));
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<AttendanceService>();
        services.AddScoped<RotationService>();
        services.AddScoped<Features.Rotation.Personal.PersonalScheduleService>();
        services.AddOptions<SessionRotationOptions>()
            .BindConfiguration(SessionRotationOptions.SectionName);

        return services;
    }
}
