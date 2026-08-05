using System.Threading;

namespace ApplyVault.Api.Infrastructure;

public interface IInterviewPrepDebugTraceContext
{
    Guid? CurrentSessionId { get; set; }
}

public sealed class InterviewPrepDebugTraceContext : IInterviewPrepDebugTraceContext
{
    private readonly AsyncLocal<Guid?> _currentSessionId = new();

    public Guid? CurrentSessionId
    {
        get => _currentSessionId.Value;
        set => _currentSessionId.Value = value;
    }
}

