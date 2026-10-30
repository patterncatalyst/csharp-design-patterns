namespace ConsignmentTracking;

/// <summary>
/// A source of draws in [0.0, 1.0) for <see cref="ProbabilisticTransitMachine"/>.
/// Abstracted behind an interface so a test can substitute a scripted draw
/// instead of depending on <see cref="System.Random"/> directly.
/// </summary>
public interface IRandomSource
{
    double NextDouble();
}
