using System.CommandLine;
using Umbraco.Community.Licensing.Tool.Data;

namespace Umbraco.Community.Licensing.Tool.Tests;

/// <summary>
/// Runs <see cref="ToolApp"/> with scripted stdin and hidden keys, capturing stdout and stderr separately.
/// Each instance has its own home and current directory under the system temp folder (created on demand,
/// removed on dispose), so no test touches the real home or the process's working directory.
/// </summary>
internal sealed class ScriptedTool : IDisposable
{
    private readonly StringWriter _stdout = new();
    private readonly StringWriter _stderr = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ucl-test-" + Guid.NewGuid().ToString("N"));
    private readonly Action<ToolContext>? _setup;

    public ScriptedTool(
        string stdin = "",
        bool stdinRedirected = true,
        string hiddenKeys = "",
        Action<ToolContext>? setup = null,
        IReadOnlyDictionary<string, string>? variables = null,
        byte[]? stdinBytes = null)
    {
        _setup = setup;
        var keys = new Queue<char>(hiddenKeys);
        Func<ConsoleKeyInfo> readKey = () =>
            {
                var c = keys.Dequeue();
                return c switch
                {
                    '\n' => new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false),
                    '\b' => new ConsoleKeyInfo('\b', ConsoleKey.Backspace, false, false, false),
                    _ => new ConsoleKeyInfo(c, ConsoleKey.A, false, false, false),
                };
            };
        var console = stdinBytes is null
            ? new ToolConsole(new StringReader(stdin), _stdout, _stderr, stdinRedirected, readKey)
            : ToolConsole.FromInputStream(new MemoryStream(stdinBytes), _stdout, _stderr, stdinRedirected, readKey);
        Environment = new ToolEnvironment(
            console,
            Clock,
            Home: Path.Combine(_root, "home"),
            CurrentDirectory: Path.Combine(_root, "cwd"),
            GetVariable: name => variables is not null && variables.TryGetValue(name, out var value) ? value : null,
            Pbkdf2Iterations: 1_000);
    }

    /// <summary>The tool's clock; fixed until advanced.</summary>
    public FixedTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 1, 15, 9, 30, 0, TimeSpan.Zero));

    public ToolEnvironment Environment { get; }

    /// <summary>The home's <c>.ucl</c> folder (the default installation location).</summary>
    public string HomeInstallation => Path.Combine(Environment.Home, Installation.HomeFolderName);

    public string Stdout => _stdout.ToString();

    public string Stderr => _stderr.ToString();

    public int Run(params string[] args) => ToolApp.Run(args, Environment, [Stub, StubTwo, StubHidden, StubThrow, StubKey], _setup);

    /// <summary>
    /// Makes <paramref name="folder"/> an installation for resolution tests: an empty file is enough to mark
    /// one. Tests of the database itself use <see cref="Installation.Open"/>.
    /// </summary>
    public static string MakeInstallation(string folder)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, Installation.DatabaseFileName), []);
        return Path.GetFullPath(folder);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>Stub standing in for "license issue": requires <c>--expires</c>, writes it to stdout.</summary>
    private static Command Stub(ToolHost host)
    {
        var expires = new Option<string?>("--expires");
        var command = new Command("stub-issue") { expires };
        host.SetAction(command, context =>
        {
            var value = context.Require("--expires", context.ParseResult.GetValue(expires), "Expiry");
            context.Console.Out.WriteLine(value);
            return ExitCodes.Done;
        });
        return command;
    }

    /// <summary>Stub needing two inputs: writes both to stdout, comma-separated.</summary>
    private static Command StubTwo(ToolHost host)
    {
        var product = new Option<string?>("--product");
        var customer = new Option<string?>("--customer");
        var command = new Command("stub-two") { product, customer };
        host.SetAction(command, context =>
        {
            var values = context.RequireAll(
                new RequiredInput("--product", context.ParseResult.GetValue(product), "Product"),
                new RequiredInput("--customer", context.ParseResult.GetValue(customer), "Customer"));
            context.Console.Out.WriteLine(string.Join(",", values));
            return ExitCodes.Done;
        });
        return command;
    }

    /// <summary>Stub throwing the exception family named by its argument.</summary>
    private static Command StubThrow(ToolHost host)
    {
        var kind = new Argument<string>("kind");
        var command = new Command("stub-throw") { kind };
        host.SetAction(command, context => throw ErrorHandlingTests.Make(context.ParseResult.GetValue(kind)!));
        return command;
    }

    /// <summary>
    /// Stub standing in for a key-creating command: opens the database, unlocks with the passphrase, then
    /// creates one signing key. Writes the key ID to stdout.
    /// </summary>
    private static Command StubKey(ToolHost host)
    {
        var command = new Command("stub-key");
        host.SetAction(command, context =>
        {
            using var db = context.OpenDatabase();
            var passphrase = context.ReadVerifiedPassphrase(db);
            using var pair = SigningKeyPair.Create();
            db.SigningKeys.Add(new SigningKey
            {
                SigningKeyId = pair.SigningKeyId,
                PublicKey = pair.PublicKey.Export(),
                EncryptedPrivateKeyPem = KeyProtection.Encrypt(pair.PrivateKey, passphrase, context.Environment.Pbkdf2Iterations),
                Role = SigningKeyRole.Active,
                CreatedUtc = context.Environment.TimeProvider.GetUtcNow(),
            });
            db.SaveChanges();
            context.Console.Out.WriteLine(pair.SigningKeyId);
            return ExitCodes.Done;
        });
        return command;
    }

    /// <summary>Stub reading a hidden value through the context: writes it to stdout.</summary>
    private static Command StubHidden(ToolHost host)
    {
        var command = new Command("stub-hidden");
        host.SetAction(command, context =>
        {
            context.Console.Out.WriteLine(context.PromptHidden("Passphrase: "));
            return ExitCodes.Done;
        });
        return command;
    }
}
