using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Requests;
using Microsoft.AspNetCore.Http.Features;
using System.Globalization;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Application.Features.Ledger.CreateExpense;
using LegendsTeamVN.BadmintonClub.Application.Features.Ledger.IssueMonthlyContribution;
using LegendsTeamVN.BadmintonClub.Application.Features.Ledger.RecordMonthlyContribution;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Responses;
using LegendsTeamVN.BadmintonClub.Application.Features.Ledger.GetAttachment;
using LegendsTeamVN.BadmintonClub.Application.Features.Ledger.GetOverview;
using LegendsTeamVN.BadmintonClub.Application.Features.Ledger.ListDebts;
using LegendsTeamVN.BadmintonClub.Application.Features.Ledger.ListMonthlyContributionCandidates;
using LegendsTeamVN.BadmintonClub.Application.Features.Ledger.ListTransactions;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Presentation.Security;
using LegendsTeamVN.Core.Presentation.Abstractions;
using LegendsTeamVN.Core.Presentation.Extensions;
using LegendsTeamVN.Core.Utilities.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace LegendsTeamVN.BadmintonClub.Presentation.Endpoints;

public sealed class LedgerEndpoint : EndpointGroupBase
{
    public static void ConfigureSwagger(SwaggerGenOptions options) => options.OperationFilter<LedgerOperationFilter>();

    protected override string Name => "clubs/{clubId:guid}/ledger";

    protected override void Map(RouteGroupBuilder group)
    {
        group.RequireAuthorization("SessionOperations");
        group.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            http.Response.Headers.CacheControl = "no-store";
            http.Response.Headers["X-Content-Type-Options"] = "nosniff";
            if (http.Request.Headers.TryGetValue("X-Group-Id", out var header) &&
                (!Guid.TryParse(header.ToString(), out var headerId) ||
                 !Guid.TryParse(http.Request.RouteValues["clubId"]?.ToString(), out var routeId) || headerId != routeId))
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden,
                    detail: "Ngữ cảnh nhóm không khớp với đường dẫn.",
                    extensions: new Dictionary<string, object?> { ["code"] = "Ledger.GroupContextMismatch" });
            return await next(context);
        });
        group.MapPost("transactions", CreateExpense).RequireGroupPermission(GroupPermissions.Finance.LedgerManage)
            .WithMetadata(new RequestSizeLimitAttribute(6 * 1024 * 1024)).Produces<LedgerWriteResponse>(200).ProducesProblem(400).ProducesProblem(403).ProducesProblem(409).ProducesProblem(413).ProducesProblem(415).ProducesProblem(503).WithName("CreateLedgerExpense");
        group.MapPost("monthly-contributions", IssueMonth).RequireGroupPermission(GroupPermissions.Finance.LedgerManage).Produces<LedgerWriteResponse>(200).ProducesProblem(400).ProducesProblem(403).ProducesProblem(409).ProducesProblem(503).WithName("IssueMonthlyContribution");
        group.MapPost("monthly-contributions/{monthlyContributionId:guid}/collect", CollectMonth)
            .RequireGroupPermission(GroupPermissions.Finance.LedgerManage).Produces<LedgerWriteResponse>(200).ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409).ProducesProblem(503).WithName("RecordMonthlyContribution");
        group.MapGet("", Overview).RequireGroupPermission(GroupPermissions.Finance.LedgerView).Produces<LedgerOverviewResponse>(200).ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(503).WithName("GetLedgerOverview");
        group.MapGet("transactions", Transactions).RequireGroupPermission(GroupPermissions.Finance.LedgerView).Produces<LedgerPageResponse<LedgerTransactionResponse>>(200).ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(503).WithName("ListLedgerTransactions");
        group.MapGet("debts", Debts).RequireGroupPermission(GroupPermissions.Finance.LedgerView).Produces<LedgerPageResponse<LedgerDebtResponse>>(200).ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(503).WithName("ListLedgerDebts");
        group.MapGet("monthly-contributions/candidates", Candidates).RequireGroupPermission(GroupPermissions.Finance.LedgerManage).Produces<LedgerPageResponse<MonthlyContributionCandidateResponse>>(200).ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(503).WithName("ListMonthlyContributionCandidates");
        group.MapGet("transactions/{ledgerId:guid}/attachment", Attachment).RequireGroupPermission(GroupPermissions.Finance.LedgerView).Produces(200, contentType: "application/octet-stream").ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(503).WithName("GetLedgerAttachment");
    }


    private static async Task<IResult> IssueMonth(Guid clubId, IssueMonthlyContributionRequest request, ISender sender, CancellationToken ct) =>
        Respond(await sender.Send(new IssueMonthlyContributionCommand(clubId, request.UserId, request.ContributionMonth), ct));

    private static async Task<IResult> CollectMonth(Guid clubId, Guid monthlyContributionId, RecordMonthlyContributionRequest request, ISender sender, CancellationToken ct) =>
        Respond(await sender.Send(new RecordMonthlyContributionCommand(clubId, monthlyContributionId, request.RequestId, request.ExpectedAmount), ct));

    private static async Task<IResult> CreateExpense(Guid clubId, HttpRequest request, ISender sender, CancellationToken ct)
    {
        if (!request.HasFormContentType) return Results.Problem(statusCode: 415, detail: "Khoản chi cần gửi dạng multipart/form-data.");
        try
        {
            const long maximumBodyLength = 6 * 1024 * 1024;
            if (request.ContentLength > maximumBodyLength) return FileTooLarge();
            var size = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (size is { IsReadOnly: false }) size.MaxRequestBodySize = maximumBodyLength;
            var form = await request.ReadFormAsync(new FormOptions { MultipartBodyLengthLimit = maximumBodyLength, ValueLengthLimit = 4096 }, ct);
            if (!Guid.TryParse(form["requestId"], out var requestId) ||
                !decimal.TryParse(form["amount"], NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var amount) ||
                !Enum.TryParse<LedgerCategory>(form["category"], out var category) ||
                form.Files.Count > 1 || form.Files.Any(f => f.Name != "invoice"))
                return Results.Problem(statusCode: 400, detail: "Dữ liệu khoản chi không hợp lệ.");
            Guid? sessionId = null;
            if (!string.IsNullOrEmpty(form["sessionId"]))
            {
                if (!Guid.TryParse(form["sessionId"], out var session)) return Results.Problem(statusCode: 400, detail: "Mã buổi chơi không hợp lệ.");
                sessionId = session;
            }
            var file = form.Files.SingleOrDefault();
            byte[]? content = null;
            if (file is not null)
            {
                if (file.Length > LedgerAttachment.MaximumContentLength) return FileTooLarge();
                await using var stream = file.OpenReadStream();
                using var buffer = new MemoryStream();
                var chunk = new byte[81920];
                int read;
                while ((read = await stream.ReadAsync(chunk, ct)) > 0)
                {
                    if (buffer.Length + read > LedgerAttachment.MaximumContentLength) return FileTooLarge();
                    await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
                }
                content = buffer.ToArray();
            }
            return Respond(await sender.Send(new CreateLedgerExpenseCommand(clubId, requestId, amount, category,
                form["description"].ToString(), sessionId, file?.FileName, file?.ContentType, content), ct));
        }
        catch (InvalidDataException) { return Results.Problem(statusCode: 400, detail: "Dữ liệu multipart không hợp lệ."); }
        catch (BadHttpRequestException ex) when (ex.StatusCode == 413) { return FileTooLarge(); }
    }

    private static IResult FileTooLarge() => Results.Problem(statusCode: 413, detail: "Hóa đơn tối đa 5 MiB.");

    private static async Task<IResult> Overview(Guid clubId, ISender sender, DateTime? fromUtc = null,
        DateTime? toUtc = null, CancellationToken ct = default) =>
        Respond(await sender.Send(new GetLedgerOverviewQuery(clubId, new LedgerReadFilter(fromUtc, toUtc)), ct));

    private static async Task<IResult> Transactions(Guid clubId, [AsParameters] LedgerReadFilter filter, ISender sender, CancellationToken ct) =>
        Respond(await sender.Send(new ListLedgerTransactionsQuery(clubId, filter), ct));

    private static async Task<IResult> Debts(Guid clubId, [AsParameters] LedgerReadFilter filter, ISender sender, CancellationToken ct) =>
        Respond(await sender.Send(new ListLedgerDebtsQuery(clubId, filter), ct));

    private static async Task<IResult> Candidates(Guid clubId, [AsParameters] LedgerReadFilter filter, ISender sender, CancellationToken ct) =>
        Respond(await sender.Send(new ListMonthlyContributionCandidatesQuery(clubId, filter), ct));

    private static async Task<IResult> Attachment(Guid clubId, Guid ledgerId, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new GetLedgerAttachmentQuery(clubId, ledgerId), ct);
        return result.Error.Code == "Ledger.Disabled" ? Unavailable(result.Error) :
            result.Match(onSuccess: data => Results.File(data.Content, data.ContentType, data.OriginalFileName));
    }

    private static IResult Respond<T>(Result<T> result) => result.Error.Code is "Ledger.Disabled" or "Ledger.WritesDisabled" or "Ledger.LockUnavailable"
        ? Unavailable(result.Error) : result.Match(onSuccess: data => Results.Ok(data));

    private static IResult Unavailable(Error error) => Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
        detail: error.Message, extensions: new Dictionary<string, object?> { ["code"] = error.Code });

    private sealed class LedgerOperationFilter : IOperationFilter
    {
        public LedgerOperationFilter() { }

        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var name = context.ApiDescription.ActionDescriptor.EndpointMetadata.OfType<IEndpointNameMetadata>().SingleOrDefault()?.EndpointName;
            if (name == "GetLedgerAttachment")
            {
                operation.Responses["200"].Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/pdf"] = new() { Schema = new OpenApiSchema { Type = "string", Format = "binary" } },
                    ["image/png"] = new() { Schema = new OpenApiSchema { Type = "string", Format = "binary" } },
                    ["image/jpeg"] = new() { Schema = new OpenApiSchema { Type = "string", Format = "binary" } }
                };
                return;
            }
            if (name != "CreateLedgerExpense") return;
            operation.RequestBody = new OpenApiRequestBody
            {
                Required = true,
                Description = "Khoản chi có RequestId duy nhất trong nhóm; hóa đơn tùy chọn tối đa 5 MiB.",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["multipart/form-data"] = new()
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Required = new HashSet<string> { "requestId", "amount", "category", "description" },
                            Properties = new Dictionary<string, OpenApiSchema>
                            {
                                ["requestId"] = new() { Type = "string", Format = "uuid" },
                                ["amount"] = new() { Type = "number", Format = "decimal", Minimum = 0.01m, Maximum = 9999999999999999.99m, MultipleOf = 0.01m },
                                ["category"] = new() { Type = "string", Enum = new List<Microsoft.OpenApi.Any.IOpenApiAny>
                                    { new Microsoft.OpenApi.Any.OpenApiString("COURT_RENT"), new Microsoft.OpenApi.Any.OpenApiString("SHUTTLE_EXPENSE"), new Microsoft.OpenApi.Any.OpenApiString("OTHER") } },
                                ["description"] = new() { Type = "string", MaxLength = 1000 },
                                ["sessionId"] = new() { Type = "string", Format = "uuid", Nullable = true },
                                ["invoice"] = new() { Type = "string", Format = "binary", Description = "JPEG, PNG hoặc PDF, tối đa 5 MiB; cùng tệp khi retry cùng RequestId." }
                            }
                        }
                    }
                }
            };
        }
    }
}
