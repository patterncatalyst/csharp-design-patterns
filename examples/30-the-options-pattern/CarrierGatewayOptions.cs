namespace CarrierConfiguration;

/// <summary>
/// Settings for a single outbound carrier gateway: where to reach it, how long
/// to wait, and how many retries a caller gets before giving up. A settable
/// class with a public parameterless constructor, not a record, because the
/// configuration binder populates it by setting properties after construction
/// rather than through a constructor.
/// </summary>
public sealed class CarrierGatewayOptions
{
    /// <summary>
    /// The configuration section this type binds from, rooted at the
    /// application's top-level configuration.
    /// </summary>
    public const string SectionName = "CarrierGateway";

    /// <summary>
    /// The carrier gateway's base address. Must be an absolute URI; validated
    /// by <see cref="CarrierGatewayOptionsValidator"/>, not by this type.
    /// </summary>
    public string Endpoint { get; set; } = "";

    /// <summary>
    /// How long a single call to the gateway is allowed to run before it is
    /// treated as failed.
    /// </summary>
    public int TimeoutSeconds { get; set; }

    /// <summary>
    /// How many retries a caller gets after the first failed attempt.
    /// </summary>
    public int RetryBudget { get; set; }
}
