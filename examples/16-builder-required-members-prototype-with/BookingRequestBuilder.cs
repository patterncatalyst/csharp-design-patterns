namespace BookingRequests;

/// <summary>
/// The classic GoF Builder, fluent and mutable, kept alongside the
/// required-member object initializer it stands next to in this example.
/// <see cref="Build"/> is where the two idioms part ways: an object
/// initializer that omits a required member fails at compile time, while
/// this builder only discovers a missing field when <see cref="Build"/>
/// runs, and reports it with <see cref="BookingValidationException"/> rather
/// than a compiler error.
/// </summary>
public sealed class BookingRequestBuilder
{
    private readonly List<LineItem> _lineItems = [];
    private string? _origin;
    private string? _destination;
    private Money? _declaredValue;
    private string _serviceLevel = "standard";
    private bool _insuranceRequested;
    private string? _specialInstructions;
    private string? _customerReference;

    public BookingRequestBuilder WithOrigin(string origin)
    {
        _origin = origin;
        return this;
    }

    public BookingRequestBuilder WithDestination(string destination)
    {
        _destination = destination;
        return this;
    }

    public BookingRequestBuilder WithDeclaredValue(Money declaredValue)
    {
        _declaredValue = declaredValue;
        return this;
    }

    public BookingRequestBuilder WithServiceLevel(string serviceLevel)
    {
        _serviceLevel = serviceLevel;
        return this;
    }

    public BookingRequestBuilder AddLineItem(LineItem lineItem)
    {
        _lineItems.Add(lineItem);
        return this;
    }

    public BookingRequestBuilder RequestInsurance()
    {
        _insuranceRequested = true;
        return this;
    }

    public BookingRequestBuilder WithSpecialInstructions(string specialInstructions)
    {
        _specialInstructions = specialInstructions;
        return this;
    }

    public BookingRequestBuilder WithCustomerReference(string customerReference)
    {
        _customerReference = customerReference;
        return this;
    }

    /// <summary>
    /// Validates the accumulated state and produces a
    /// <see cref="BookingRequest"/>, or throws
    /// <see cref="BookingValidationException"/> if a required field is
    /// missing or invalid.
    /// </summary>
    public BookingRequest Build()
    {
        if (string.IsNullOrWhiteSpace(_origin))
        {
            throw new BookingValidationException("A booking request needs a non-empty origin.");
        }

        if (string.IsNullOrWhiteSpace(_destination))
        {
            throw new BookingValidationException("A booking request needs a non-empty destination.");
        }

        if (_declaredValue is not { Amount: >= 0m })
        {
            throw new BookingValidationException("A booking request needs a declared value.");
        }

        return new BookingRequest
        {
            Origin = _origin,
            Destination = _destination,
            DeclaredValue = _declaredValue.Value,
            ServiceLevel = _serviceLevel,
            LineItems = _lineItems.Count == 0 ? [] : [.. _lineItems],
            InsuranceRequested = _insuranceRequested,
            SpecialInstructions = _specialInstructions,
            CustomerReference = _customerReference,
        };
    }
}
