using System.Diagnostics;
using System.Runtime.InteropServices;
using Umbraco.Community.Licensing.Core.Tests.Support;
using static Umbraco.Community.Licensing.Core.Tests.Evaluation.EvaluationSupport;

namespace Umbraco.Community.Licensing.Core.Tests.Evaluation;

/// <summary>Runs the performance tests alone: the timing and process-wide memory measures are disturbed by parallel test classes.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class PerformanceCollection
{
    public const string Name = "Performance";
}

/// <summary>Per-request cost, memory and snapshot behaviour of evaluation (PDR-0022, ADR-0005).</summary>
[Trait("Category", "Performance")]
[Collection(PerformanceCollection.Name)]
public class PerformanceTests
{
    private const int TargetLength = 500;

    private const int Warmup = 1_000;

    private const int Timed = 10_000;

    private const long MemoryBudgetBytes = 1024 * 1024;

    private readonly ITestOutputHelper _output;

    public PerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        _output.WriteLine(
            $"Platform: {RuntimeInformation.OSDescription}, {RuntimeInformation.OSArchitecture}, " +
            $"{Environment.ProcessorCount} processors, {RuntimeInformation.FrameworkDescription}");
    }

    /// <summary>An ASCII-typical key close to 500 characters: switch features pad the example-2 shape.</summary>
    private static string KeyOf500Characters()
    {
        var features = FeatureList.Empty.With("ecommerce", true).With("max-orders", 500m);
        var key = Issue(Request(features: features));
        for (var i = 0; key.Length < TargetLength - 5; i++)
        {
            features = features.With($"feature-{i:D2}", true);
            key = Issue(Request(features: features));
        }

        return key;
    }

    [Fact]
    public void PerRequestCost_ValidKeyOf500Characters_AveragesUnderOneMillisecond()
    {
        var key = KeyOf500Characters();
        var evaluator = Evaluator();
        _output.WriteLine($"Key length: {key.Length}");
        Assert.InRange(key.Length, TargetLength - 5, TargetLength + 5);
        Assert.Equal(LicenseState.Valid, evaluator.Evaluate(key).State);

        Run(evaluator, key, Warmup);

        var started = Stopwatch.GetTimestamp();
        var checksum = Run(evaluator, key, Timed);

        var average = Stopwatch.GetElapsedTime(started) / Timed;
        _output.WriteLine($"Average per evaluation over {Timed:N0}: {average.TotalMilliseconds:F4} ms");
        Assert.Equal((long)Timed * (int)LicenseState.Valid, checksum);
        Assert.True(average < TimeSpan.FromMilliseconds(1), $"Average {average.TotalMilliseconds:F4} ms is not under 1 ms.");
    }

    [Fact]
    public void NoMemoryGrowth_AfterManyEvaluations_HeldMemoryDoesNotGrowWithCalls()
    {
        var key = KeyOf500Characters();
        var evaluator = Evaluator();
        Run(evaluator, key, Warmup);

        var checksum = Run(evaluator, key, 1_000);
        var afterThousand = GC.GetTotalMemory(true);
        checksum += Run(evaluator, key, 99_000);
        var afterHundredThousand = GC.GetTotalMemory(true);

        _output.WriteLine($"Memory after 1,000 evaluations: {afterThousand:N0} bytes; after 100,000 in total: {afterHundredThousand:N0} bytes");
        Assert.Equal(100_000L * (int)LicenseState.Valid, checksum);
        Assert.True(
            Math.Abs(afterHundredThousand - afterThousand) < MemoryBudgetBytes,
            $"Memory differs by {afterHundredThousand - afterThousand:N0} bytes.");
    }

    [Fact]
    public void AResultIsASnapshot_KeptValidResultIsUnchangedByLaterEvaluations()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2027, 3, 1, 12, 0, 0, TimeSpan.Zero));
        var evaluator = new LicenseEvaluator(Product, Trusted, clock);
        var key = Issue(Request(expiry: LicenseExpiry.At(new DateTimeOffset(2027, 3, 1, 12, 0, 0, TimeSpan.Zero))));
        var kept = evaluator.Evaluate(key);
        var licenseBefore = kept.License;

        clock.SetUtcNow(new DateTimeOffset(2027, 3, 1, 12, 0, 1, TimeSpan.Zero));
        Run(evaluator, key, 100);

        Assert.Equal(LicenseState.Valid, kept.State);
        Assert.True(kept.IsLicensed);
        Assert.Same(licenseBefore, kept.License);
        Assert.Equal(LicenseState.Expired, evaluator.Evaluate(key).State);
    }

    /// <summary>Evaluates repeatedly and sums the states, so every result is consumed and none can be optimised away.</summary>
    private static long Run(LicenseEvaluator evaluator, string key, int count)
    {
        long checksum = 0;
        for (var i = 0; i < count; i++)
        {
            checksum += (int)evaluator.Evaluate(key).State;
        }

        return checksum;
    }
}
