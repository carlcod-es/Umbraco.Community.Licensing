namespace Umbraco.Community.Licensing.Tool.Tests;

/// <summary>A unique folder under the system temp folder, deleted on dispose (fails if a handle lingers).</summary>
internal sealed class TempFolder : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ucl-test-" + Guid.NewGuid().ToString("N"));

    public TempFolder() => Directory.CreateDirectory(Path);

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
