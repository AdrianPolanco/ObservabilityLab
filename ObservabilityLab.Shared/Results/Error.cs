

namespace ObservabilityLab.Shared.Results
{
    public record Error(string Code, string Message, Dictionary<string, object>? Metadata = null);
}
