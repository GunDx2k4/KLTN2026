using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Responses;
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
        services.AddScoped<Features.Ledger.LedgerWriteSupport>();
        transactionIndex = services.ToList().FindIndex(d => d.ServiceType == typeof(MediatR.IPipelineBehavior<,>) &&
            d.ImplementationType == typeof(LegendsTeamVN.Core.Application.Behaviors.UnitOfWorkBehavior<,>));
        services.Insert(transactionIndex, ServiceDescriptor.Transient(typeof(MediatR.IPipelineBehavior<Features.Ledger.CreateExpense.CreateLedgerExpenseCommand, LegendsTeamVN.Core.Utilities.Results.Result<DTOs.Ledger.Responses.LedgerWriteResponse>>), typeof(Features.Ledger.LedgerLockBehavior<Features.Ledger.CreateExpense.CreateLedgerExpenseCommand>)));
        services.Insert(transactionIndex, ServiceDescriptor.Transient(typeof(MediatR.IPipelineBehavior<Features.Ledger.IssueMonthlyContribution.IssueMonthlyContributionCommand, LegendsTeamVN.Core.Utilities.Results.Result<DTOs.Ledger.Responses.LedgerWriteResponse>>), typeof(Features.Ledger.LedgerLockBehavior<Features.Ledger.IssueMonthlyContribution.IssueMonthlyContributionCommand>)));
        services.Insert(transactionIndex, ServiceDescriptor.Transient(typeof(MediatR.IPipelineBehavior<Features.Ledger.RecordMonthlyContribution.RecordMonthlyContributionCommand, LegendsTeamVN.Core.Utilities.Results.Result<DTOs.Ledger.Responses.LedgerWriteResponse>>), typeof(Features.Ledger.LedgerLockBehavior<Features.Ledger.RecordMonthlyContribution.RecordMonthlyContributionCommand>)));
        services.AddOptions<ReputationOptions>().BindConfiguration(ReputationOptions.SectionName)
            .Validate(o => o.BatchSize is >= 1 and <= 100 && o.IntervalSeconds is >= 5 and <= 300,
                "BatchSize phải từ 1 đến 100; IntervalSeconds phải từ 5 đến 300.")
            .Validate(o => o.EffectiveFromUtc.Offset == TimeSpan.Zero,
                "Cần EffectiveFromUtc UTC cố định cho UC20.").ValidateOnStart();
        services.AddOptions<LedgerOptions>().BindConfiguration(LedgerOptions.SectionName);
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
