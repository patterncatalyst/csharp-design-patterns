namespace WireContracts;

/// <summary>
/// Marks a type that is serialized across a process boundary (an HTTP, gRPC,
/// or message-queue wire contract), so the project's Roslyn analyzer can flag
/// money properties modeled as <see cref="decimal"/> instead of integer minor
/// units.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class WireContractAttribute : Attribute;
