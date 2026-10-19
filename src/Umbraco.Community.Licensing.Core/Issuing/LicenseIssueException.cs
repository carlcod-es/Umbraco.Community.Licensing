namespace Umbraco.Community.Licensing.Core;

/// <summary>Thrown when an issue request breaks one or more rules. Lists every problem; no key was produced.</summary>
public sealed class LicenseIssueException : Exception
{
    internal LicenseIssueException(IReadOnlyList<LicenseProblem> problems)
        : base(Describe(problems)) => Problems = problems;

    /// <summary>Every problem found, each naming its field.</summary>
    public IReadOnlyList<LicenseProblem> Problems { get; }

    private static string Describe(IReadOnlyList<LicenseProblem> problems) =>
        $"The license request has {problems.Count} problem(s): {string.Join(" ", problems.Select(p => $"[{p.Field}] {p.Message}"))}";
}
