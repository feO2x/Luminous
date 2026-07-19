using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Luminous.MimeTypeManagement;

/// <summary>
/// Reports all configuration violations found while building a MIME type registry.
/// </summary>
/// <remarks>
/// Inspect <see cref="Violations"/> rather than parsing <see cref="Exception.Message"/>. A single
/// exception can include duplicate memberships, invalid defaults, extension-preference errors, and
/// explicit hierarchy cycles so callers can fix the configuration in one pass.
/// </remarks>
public sealed class MimeTypeRegistryValidationException : Exception
{
    /// <summary>
    /// Creates an exception containing the supplied configuration violations.
    /// </summary>
    /// <param name="violations">Human-readable descriptions of every detected violation.</param>
    public MimeTypeRegistryValidationException(IEnumerable<string> violations)
        : this([..violations]) { }

    private MimeTypeRegistryValidationException(ImmutableArray<string> violations)
        : base(CreateMessage(violations))
    {
        Violations = violations;
    }

    /// <summary>
    /// Gets the immutable collection of individual configuration violations.
    /// </summary>
    public ImmutableArray<string> Violations { get; }

    private static string CreateMessage(ImmutableArray<string> violations)
    {
        return $"The MIME type registry configuration is invalid:{Environment.NewLine}" +
               string.Join(Environment.NewLine, violations);
    }
}
