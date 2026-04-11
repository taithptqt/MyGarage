using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Interfaces;

public interface IFunctionCallingRouter
{
    Task<FunctionRoutingResult> RouteAsync(
        long customerId,
        long? branchId,
        string intent,
        string intentStateJson,
        PromptBuildResult prompt,
        CancellationToken cancellationToken = default);
}

public class FunctionRoutingResult
{
    public string? AssistantReplyOverride { get; set; }

    public IReadOnlyList<FunctionCallDescriptor> FunctionCalls { get; set; } = [];
}

public class FunctionCallDescriptor
{
    public string ToolName { get; set; } = null!;

    public string ToolArgs { get; set; } = "{}";
}
