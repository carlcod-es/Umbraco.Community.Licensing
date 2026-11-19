using Acme.ContactForm.Licensing;
using Acme.ContactForm.Submissions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Umbraco.Community.Licensing;

namespace Acme.ContactForm.Tests;

public class LicenseStateLogTests
{
    private static readonly IReadOnlyList<DemoKeyEntry> Entries = DemoKeyFile.LoadEmbedded();

    public static TheoryData<string> KeyedEntryNames => [.. Entries.Where(e => e.Key is not null).Select(e => e.Name)];

    private sealed record Entry(LogLevel Level, string Message, IReadOnlyList<KeyValuePair<string, object?>> State);

    private sealed class CapturingLogger : ILogger<LicenseStateLog>
    {
        private readonly Lock _gate = new();
        private readonly List<Entry> _entries = [];

        public IReadOnlyList<Entry> Entries
        {
            get
            {
                lock (_gate)
                {
                    return [.. _entries];
                }
            }
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var values = state as IEnumerable<KeyValuePair<string, object?>> ?? [];
            lock (_gate)
            {
                _entries.Add(new Entry(logLevel, formatter(state, exception), [.. values]));
            }
        }
    }

    private sealed class SwitchableSource(string? key) : ILicenseKeySource
    {
        public string? Key { get; set; } = key;

        public LicenseKeyReading Read() => new(Key, "Test source");
    }

    private static string KeyOf(string name) => Entries.Single(e => e.Name == name).Key!;

    // The snapshot Check builds for a demo entry at its demo date, with a log that writes nowhere.
    private static LicenseSnapshot Evaluate(string name)
    {
        var entry = Entries.Single(e => e.Name == name);
        var clock = new TestClock(entry.DemoDate is { } date ? DemoKeyFile.ParseInstant(date) : DemoKeyFile.FixedClock);
        return new ContactFormLicensing(
            new ContactFormProduct(clock), new SwitchableSource(entry.Key), new InMemorySubmissionStore(TimeProvider.System),
            new LicenseStateLog(NullLogger<LicenseStateLog>.Instance)).Check("localhost");
    }

    private static ContactFormLicensing LicensingWith(TestClock clock, SwitchableSource source, LicenseStateLog log) =>
        new(new ContactFormProduct(clock), source, new InMemorySubmissionStore(TimeProvider.System), log);

    [Fact]
    public void Good_license_at_startup()
    {
        var logger = new CapturingLogger();
        var valid = Evaluate("Valid: full plan");
        var log = new LicenseStateLog(logger);

        log.Report(valid);
        log.Report(valid);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("Valid", entry.Message);
        Assert.Contains(valid.Result.License!.KeyIdentifier, entry.Message);
    }

    [Fact]
    public void Expiring_soon_logged_at_startup()
    {
        var logger = new CapturingLogger();
        var soon = Evaluate("Valid: expiring soon");
        Assert.True(soon.ExpiringSoon);
        var log = new LicenseStateLog(logger);

        log.Report(soon);
        log.Report(soon);
        log.Report(soon);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("2030-01-31", entry.Message);
        Assert.Contains(soon.Result.License!.KeyIdentifier, entry.Message);
    }

    [Fact]
    public void Expiring_soon_logged_when_the_clock_crosses_into_30_days()
    {
        var logger = new CapturingLogger();
        // The key expires 2030-01-31T23:59:59Z; 32 days left at the start.
        var clock = new TestClock(DemoKeyFile.ParseInstant("2029-12-30T12:00:00Z"));
        var licensing = LicensingWith(clock, new SwitchableSource(KeyOf("Valid: expiring soon")), new LicenseStateLog(logger));

        licensing.Check("localhost");
        licensing.Check("localhost");
        var first = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, first.Level);

        clock.Advance(TimeSpan.FromDays(2));
        var snapshot = licensing.Check("localhost");
        Assert.True(snapshot.ExpiringSoon);
        for (var i = 0; i < 3; i++)
        {
            licensing.Check("localhost");
        }

        Assert.Equal(2, logger.Entries.Count);
        Assert.Equal(LogLevel.Warning, logger.Entries[1].Level);
        Assert.Contains("2030-01-31", logger.Entries[1].Message);
        Assert.Contains(snapshot.Result.License!.KeyIdentifier, logger.Entries[1].Message);

        clock.Advance(TimeSpan.FromDays(1));
        licensing.Check("localhost");
        Assert.Equal(2, logger.Entries.Count);
    }

    [Theory]
    [InlineData("Valid: full plan")]
    [InlineData("Valid: small plan")]
    public void Key_expires(string validEntry)
    {
        var logger = new CapturingLogger();
        var log = new LicenseStateLog(logger);
        var valid = Evaluate(validEntry);
        var expired = Evaluate("Expired");

        log.Report(valid);
        log.Report(valid);
        Assert.Single(logger.Entries);

        for (var i = 0; i < 5; i++)
        {
            log.Report(expired);
        }

        Assert.Equal(2, logger.Entries.Count);
        var error = logger.Entries[1];
        Assert.Equal(LogLevel.Error, error.Level);
        Assert.Contains("Expired", error.Message);
        Assert.Contains(expired.Result.License!.KeyIdentifier, error.Message);
    }

    [Fact]
    public void Key_expires_through_Check()
    {
        var logger = new CapturingLogger();
        var clock = new TestClock(DemoKeyFile.FixedClock);
        var source = new SwitchableSource(KeyOf("Valid: full plan"));
        var licensing = LicensingWith(clock, source, new LicenseStateLog(logger));

        licensing.Check("localhost");
        licensing.Check("localhost");
        Assert.Single(logger.Entries);

        source.Key = KeyOf("Expired");
        for (var i = 0; i < 4; i++)
        {
            licensing.Check("localhost");
        }

        Assert.Equal(2, logger.Entries.Count);
        Assert.Equal(LogLevel.Information, logger.Entries[0].Level);
        Assert.Equal(LogLevel.Error, logger.Entries[1].Level);
        Assert.Contains("Expired", logger.Entries[1].Message);
        Assert.Contains(Evaluate("Expired").Result.License!.KeyIdentifier, logger.Entries[1].Message);
    }

    [Fact]
    public void First_report_is_logged_and_no_key_is_named_none()
    {
        var logger = new CapturingLogger();
        new LicenseStateLog(logger).Report(Evaluate("No key"));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains("Missing", entry.Message);
        Assert.Contains("none", entry.Message);
    }

    [Fact]
    public void An_unverified_key_logs_its_claimed_identifier()
    {
        var logger = new CapturingLogger();
        var snapshot = Evaluate("Edited key");
        var claimed = snapshot.Result.ClaimedKeyIdentifier;
        Assert.NotNull(claimed);
        new LicenseStateLog(logger).Report(snapshot);

        Assert.Contains(claimed, Assert.Single(logger.Entries).Message);
    }

    [Theory]
    [MemberData(nameof(KeyedEntryNames))]
    public void Log_holds_no_key(string name)
    {
        var key = KeyOf(name);
        var logger = new CapturingLogger();
        var log = new LicenseStateLog(logger);

        log.Report(Evaluate("No key"));
        log.Report(Evaluate(name));

        Assert.NotEmpty(logger.Entries);
        foreach (var entry in logger.Entries)
        {
            Assert.DoesNotContain(key, entry.Message);
            foreach (var (_, value) in entry.State)
            {
                Assert.DoesNotContain(key, value?.ToString() ?? "");
            }
        }
    }

    [Fact]
    public void Two_threads_report_the_same_change_and_it_is_logged_once()
    {
        var valid = Evaluate("Valid: full plan");
        var expired = Evaluate("Expired");

        for (var i = 0; i < 300; i++)
        {
            var logger = new CapturingLogger();
            var log = new LicenseStateLog(logger);
            log.Report(valid);

            using var barrier = new Barrier(4);
            var threads = Enumerable.Range(0, 4).Select(_ => new Thread(() =>
            {
                barrier.SignalAndWait();
                log.Report(expired);
            })).ToArray();
            foreach (var thread in threads)
            {
                thread.Start();
            }

            foreach (var thread in threads)
            {
                thread.Join();
            }

            Assert.Equal(2, logger.Entries.Count);
        }
    }
}
