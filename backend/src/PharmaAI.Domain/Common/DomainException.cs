namespace PharmaAI.Domain.Common;

/// <summary>A business rule was violated. <see cref="Code"/> is a stable identifier the UI can translate.</summary>
public class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
