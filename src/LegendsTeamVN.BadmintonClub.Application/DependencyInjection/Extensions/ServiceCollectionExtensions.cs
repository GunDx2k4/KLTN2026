using LegendsTeamVN.BadmintonClub.Application.DTOs.Settlement.Responses;
using LegendsTeamVN.BadmintonClub.Application.Features.Settlement.Approve;
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
        services.Insert(transactionIndex, ServiceDescriptor.Transient(typeof(MediatR.IPipelineBehavior<ApproveSettlementCommand, LegendsTeamVN.Core.Utilities.Results.Result<SessionSettlementResponse>>),
            typeof(SettlementLockBehavior)));
        transactionIndex = services.ToList().FindIndex(descriptor => descriptor.ServiceType == typeof(MediatR.IPipelineBehavior<,>) &&
            descriptor.ImplementationType == typeof(LegendsTeamVN.Core.Application.Behaviors.UnitOfWorkBehavior<,>));
        services.Insert(transactionIndex, ServiceDescriptor.Transient(typeof(MediatR.IPipelineBehavior<Features.Payments.Reconciliation.Webhook.ProcessSepayWebhookCommand,
            LegendsTeamVN.Core.Utilities.Results.Result<DTOs.Payments.Responses.SepayWebhookResponse>>), typeof(Features.Payments.Reconciliation.Webhook.SepayWebhookSecurityBehavior)));
        services.Insert(transactionIndex + 1, ServiceDescriptor.Transient(typeof(MediatR.IPipelineBehavior<Features.Payments.Reconciliation.Webhook.ProcessSepayWebhookCommand,
            LegendsTeamVN.Core.Utilities.Results.Result<DTOs.Payments.Responses.SepayWebhookResponse>>), typeof(Features.Payments.Reconciliation.Webhook.SepayWebhookLockBehavior)));
        services.AddOptions<ReconciliationOptions>().BindConfiguration(ReconciliationOptions.SectionName);
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<AttendanceService>();
        services.AddScoped<RotationService>();
        services.AddScoped<Features.Rotation.Personal.PersonalScheduleService>();
        services.AddOptions<SessionRotationOptions>()
            .BindConfiguration(SessionRotationOptions.SectionName);

        return services;
    }
}
