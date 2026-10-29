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
    private readonly string _root;
    private readonly bool _ownsRoot;
    private readonly Action<ToolContext>? _setup;

    public ScriptedTool(
        string stdin = "",
        bool stdinRedirected = true,
        string hiddenKeys = "",
        Action<ToolContext>? setup = null,
        IReadOnlyDictionary<string, string>? variables = null,
        byte[]? stdinBytes = null,
        TextReader? stdinReader = null)
        : this(null, null, stdin, stdinRedirected, hiddenKeys, setup, variables, stdinBytes, stdinReader)
    {
    }

    private ScriptedTool(
        string? root,
        FixedTimeProvider? clock,
        string stdin,
        bool stdinRedirected,
        string hiddenKeys,
        Action<ToolContext>? setup,
        IReadOnlyDictionary<string, string>? variables,
        byte[]? stdinBytes,
        TextReader? stdinReader = null)
    {
        _ownsRoot = root is null;
        _root = root ?? Path.Combine(Path.GetTempPath(), "ucl-test-" + Guid.NewGuid().ToString("N"));
        _setup = setup;
        Clock = clock ?? new FixedTimeProvider(new DateTimeOffset(2026, 1, 15, 9, 30, 0, TimeSpan.Zero));
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
            ? new ToolConsole(stdinReader ?? new StringReader(stdin), _stdout, _stderr, stdinRedirected, readKey)
            : ToolConsole.FromInputStream(new MemoryStream(stdinBytes), _stdout, _stderr, stdinRedirected, readKey);
        Environment = new ToolEnvironment(
            console,
            Clock,
            Home: Path.Combine(_root, "home"),
            CurrentDirectory: Path.Combine(_root, "cwd"),
            GetVariable: name => variables is not null && variables.TryGetValue(name, out var value) ? value : null,
            Pbkdf2Iterations: 1_000);
    }

    /// <summary>The tool's clock; fixed until advanced. <see cref="Another"/> shares it.</summary>
    public FixedTimeProvider Clock { get; }

    public ToolEnvironment Environment { get; }

    /// <summary>The home's <c>.ucl</c> folder (the default installation location).</summary>
    public string HomeInstallation => Path.Combine(Environment.Home, Installation.HomeFolderName);

    public string Stdout => _stdout.ToString();

    public string Stderr => _stderr.ToString();

    public int Run(params string[] args) => ToolApp.Run(args, Environment, [Stub, StubTwo, StubHidden, StubThrow, StubKey], _setup);

    /// <summary>
    /// Makes <paramref name="folder"/> an installation for resolution tests: an empty file is enough to mark
    /// one. Tests of the database itself use <see cref="Installation.Open(string)"/>; tests of commands use
    /// <see cref="Installed"/>.
    /// </summary>
    public static string MakeInstallation(string folder)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, Installation.DatabaseFileName), []);
        return Path.GetFullPath(folder);
    }

    /// <summary>The passphrase <see cref="Installed"/> sets.</summary>
    public const string DefaultPassphrase = "correct horse battery staple";

    /// <summary>
    /// A tool with a new installation made by the real <c>ucl setup</c>, answered through the scripted
    /// interactive console. The returned tool (default: nobody at a terminal) owns the folders; use
    /// <see cref="Another"/> for further runs with other stdin, keys or variables.
    /// </summary>
    public static ScriptedTool Installed(SetupAnswers? answers = null)
    {
        var tool = new ScriptedTool();
        try
        {
            using var setup = tool.Another(stdin: (answers ?? new SetupAnswers()).Stdin, stdinRedirected: false, hiddenKeys: (answers ?? new SetupAnswers()).HiddenKeys);
            var exit = setup.Run("setup");
            if (exit != 0)
            {
                throw new InvalidOperationException($"Setup exited {exit}: {setup.Stderr}");
            }

            return tool;
        }
        catch
        {
            tool.Dispose();
            throw;
        }
    }

    /// <summary>
    /// A new tool (fresh streams, own stdin, keys and variables) over the same home, current directory and clock.
    /// It does not remove the folders on dispose; the tool that made them does.
    /// </summary>
    public ScriptedTool Another(
        string stdin = "",
        bool stdinRedirected = true,
        string hiddenKeys = "",
        Action<ToolContext>? setup = null,
        IReadOnlyDictionary<string, string>? variables = null,
        byte[]? stdinBytes = null,
        TextReader? stdinReader = null) =>
        new(_root, Clock, stdin, stdinRedirected, hiddenKeys, setup, variables, stdinBytes, stdinReader);

    public void Dispose()
    {
        if (_ownsRoot && Directory.Exists(_root))
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

/// <summary>The answers <c>ucl setup</c> is given by <see cref="ScriptedTool.Installed"/>; the defaults are the proposed values.</summary>
/// <param name="Shared">One signing key shared across all products.</param>
/// <param name="Standby">Create standby keys.</param>
/// <param name="Prefix">The prefix as typed; empty accepts <c>LIC</c>.</param>
/// <param name="CurrentFolder">Choose the current folder instead of <c>~/.ucl</c>.</param>
/// <param name="Passphrase">Typed twice.</param>
internal sealed record SetupAnswers(
    bool Shared = false,
    bool Standby = true,
    string Prefix = "",
    bool CurrentFolder = false,
    string Passphrase = ScriptedTool.DefaultPassphrase)
{
    /// <summary>The answers read from stdin, in setup's order (the passphrase is typed on the hidden keys).</summary>
    public string Stdin =>
        $"{(CurrentFolder ? "2" : "")}\n{Prefix}\n{(Shared ? "2" : "")}\n{(Standby ? "" : "n")}\n";

    public string HiddenKeys => $"{Passphrase}\n{Passphrase}\n";
}
