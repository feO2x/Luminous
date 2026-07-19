using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Luminous.MimeTypeManagement;

public sealed class MimeTypeRegistryValidationException : Exception
{
    public MimeTypeRegistryValidationException(IEnumerable<string> violations)
        : this([..violations]) { }

    private MimeTypeRegistryValidationException(ImmutableArray<string> violations)
        : base(CreateMessage(violations))
    {
        Violations = violations;
    }

    public ImmutableArray<string> Violations { get; }

    private static string CreateMessage(ImmutableArray<string> violations)
    {
        return $"The MIME type registry configuration is invalid:{Environment.NewLine}" +
               string.Join(Environment.NewLine, violations);
    }
}
