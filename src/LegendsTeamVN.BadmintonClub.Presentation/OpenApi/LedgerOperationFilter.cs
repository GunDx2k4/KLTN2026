using Microsoft.AspNetCore.Routing;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LegendsTeamVN.BadmintonClub.Presentation.OpenApi;

public sealed class LedgerOperationFilter : IOperationFilter
{
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
