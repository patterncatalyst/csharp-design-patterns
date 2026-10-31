namespace DeclarationParsing;

/// <summary>
/// A customs declaration that has passed validation and is ready for the
/// duty office. Both the Tester-Doer and <see cref="Result{T}"/> sides
/// produce the same shape; they differ only in how a caller learns whether
/// production succeeded.
/// </summary>
public sealed record ProcessedDeclaration(string Reference, decimal DeclaredValue);
