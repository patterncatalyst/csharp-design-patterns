using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace CarrierManifest.Generation;

/// <summary>
/// A thin wrapper over <see cref="ImmutableArray{T}"/> that compares by value
/// (element-by-element) instead of by reference. The incremental generator
/// pipeline caches a step's output by comparing it to the previous run's
/// output with <c>Equals</c>; <see cref="ImmutableArray{T}"/> itself only ever
/// compares equal to the exact same backing array instance, so two pipeline
/// runs that reparse an unchanged manifest into an equal-but-distinct array
/// would never be seen as equal, and every downstream step (including source
/// emission) would re-run on every edit regardless of whether the content
/// actually changed. Wrapping the array here and comparing element-by-element
/// restores the caching the incremental pipeline is built around.
/// </summary>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    private readonly ImmutableArray<T> _values;

    public EquatableArray(ImmutableArray<T> values)
    {
        _values = values;
    }

    public int Length => _values.IsDefault ? 0 : _values.Length;

    private ImmutableArray<T> ValuesOrEmpty => _values.IsDefault ? ImmutableArray<T>.Empty : _values;

    public bool Equals(EquatableArray<T> other) => ValuesOrEmpty.SequenceEqual(other.ValuesOrEmpty);

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = 17;
        foreach (var value in ValuesOrEmpty)
        {
            hash = (hash * 31) + value.GetHashCode();
        }

        return hash;
    }

    public ImmutableArray<T>.Enumerator GetEnumerator() => ValuesOrEmpty.GetEnumerator();

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)ValuesOrEmpty).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)ValuesOrEmpty).GetEnumerator();

    public static implicit operator EquatableArray<T>(ImmutableArray<T> values) => new(values);

    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);

    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);
}
