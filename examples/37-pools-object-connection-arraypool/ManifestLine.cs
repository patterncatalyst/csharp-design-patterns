namespace GatewayPooling;

/// <summary>A single code/value pair within an EDI manifest.</summary>
public readonly record struct ManifestLine(string Code, string Value);
