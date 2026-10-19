namespace Umbraco.Community.Licensing.Core;

/// <summary>One problem found in an issue request or a setup value.</summary>
/// <param name="Field">The field the problem belongs to, such as <c>reference</c> or <c>features.max-orders</c>.</param>
/// <param name="Message">A human-readable description of the problem.</param>
public sealed record LicenseProblem(string Field, string Message);
