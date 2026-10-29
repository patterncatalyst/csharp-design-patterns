using Xunit;

namespace FreightValueTypes.Tests;

/// <summary>
/// Evidence, not just a claim: a steady-state parse loop allocates zero
/// bytes on the calling thread. <see cref="EdiRecordParser"/> slices the
/// input span, parses numbers straight out of those slices, and resolves
/// the currency field to an already-interned literal, so nothing escapes to
/// the heap once the JIT and any one-time static initialization have run.
/// </summary>
public sealed class NoAllocationTests
{
    private const string ValidSegment = "0042170302" + "0125.50" + "00450.75" + "USD";

    [Fact]
    public void TryParse_steady_state_allocates_nothing_on_the_current_thread()
    {
        // Warm up: let the JIT tier up and any one-time static
        // initialization (culture lookups, interned currency literals)
        // happen before the measured loop runs.
        var warmUpOk = true;
        for (var i = 0; i < 10_000; i++)
        {
            warmUpOk &= EdiRecordParser.TryParse(ValidSegment, out _);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var before = GC.GetAllocatedBytesForCurrentThread();

        var measuredOk = true;
        for (var i = 0; i < 100_000; i++)
        {
            measuredOk &= EdiRecordParser.TryParse(ValidSegment, out _);
        }

        var after = GC.GetAllocatedBytesForCurrentThread();

        Assert.True(warmUpOk);
        Assert.True(measuredOk);
        Assert.Equal(before, after);
    }
}
