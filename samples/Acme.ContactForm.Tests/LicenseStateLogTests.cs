using Acme.ContactForm.Licensing;
using Acme.ContactForm.Submissions;
using Microsoft.Extensions.Logging;
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

    private static LicenseResult Evaluate(string name)
    {
        var entry = Entries.Single(e => e.Name == name);
        var clock = new TestClock(entry.DemoDate is { } date ? DemoKeyFile.ParseInstant(date) : DemoKeyFile.FixedClock);
        return new ContactFormProduct(clock).Evaluator.Evaluate(entry.Key);
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
        var warning = logger.Entries[1];
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains("Expired", warning.Message);
        Assert.Contains(expired.License!.KeyIdentifier, warning.Message);
    }

    [Fact]
    public void Key_expires_through_Check()
    {
        var logger = new CapturingLogger();
        var clock = new TestClock(DemoKeyFile.FixedClock);
        var source = new SwitchableSource(KeyOf("Valid: full plan"));
        var licensing = new ContactFormLicensing(
            new ContactFormProduct(clock), source, new InMemorySubmissionStore(TimeProvider.System), clock, new LicenseStateLog(logger));

        licensing.Check("localhost");
        licensing.Check("localhost");
        Assert.Single(logger.Entries);

        source.Key = KeyOf("Expired");
        for (var i = 0; i < 4; i++)
        {
            licensing.Check("localhost");
        }

        Assert.Equal(2, logger.Entries.Count);
        Assert.All(logger.Entries, e => Assert.Equal(LogLevel.Warning, e.Level));
        Assert.Contains("Expired", logger.Entries[1].Message);
        Assert.Contains(Evaluate("Expired").License!.KeyIdentifier, logger.Entries[1].Message);
    }

    [Fact]
    public void First_report_is_logged_and_no_key_is_named_none()
    {
        var logger = new CapturingLogger();
        new LicenseStateLog(logger).Report(Evaluate("No key"));

        var entry = Assert.Single(logger.Entries);
        Assert.Contains("Missing", entry.Message);
        Assert.Contains("none", entry.Message);
    }

    [Fact]
    public void An_unverified_key_logs_its_claimed_identifier()
    {
        var logger = new CapturingLogger();
        var result = Evaluate("Edited key");
        Assert.NotNull(result.ClaimedKeyIdentifier);
        new LicenseStateLog(logger).Report(result);

        Assert.Contains(result.ClaimedKeyIdentifier, Assert.Single(logger.Entries).Message);
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
