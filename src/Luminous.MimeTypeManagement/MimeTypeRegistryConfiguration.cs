using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Luminous.MimeTypeManagement;

/// <summary>
/// Represents the complete, immutable configuration of a <see cref="MimeTypeRegistry" />.
/// </summary>
/// <remarks>
/// <para>
/// Instances describe every aspect of a registry: <see cref="MimeTypeRegistryBuilder.Build" /> creates
/// one and passes it to the registry, <see cref="MimeTypeRegistry.ToBuilder" /> reads it back so a
/// registry can be customized without losing any configuration, and the
/// <see cref="MimeTypeRegistry(MimeTypeRegistryConfiguration)" /> constructor accepts directly assembled
/// instances, for example after deserializing configuration data. All values are stored as configured;
/// derived data such as alias-normalized hierarchy endpoints is computed by the registry, not stored here.
/// </para>
/// <para>
/// This type carries data and does not validate cross-references between its properties. The
/// <see cref="MimeTypeRegistry(MimeTypeRegistryConfiguration)" /> constructor validates the complete
/// configuration and reports all violations in one <see cref="MimeTypeRegistryValidationException" />.
/// </para>
/// </remarks>
public sealed class MimeTypeRegistryConfiguration
{
    /// <summary>
    /// Gets the equivalence groups in registration order.
    /// </summary>
    /// <remarks>
    /// Registration order is also the default preference order when several groups claim the same file
    /// extension and no entry in <see cref="ExtensionPreferences" /> overrides it. Assigning a default
    /// array stores an empty array.
    /// </remarks>
    public required ImmutableArray<MimeTypeGroup> Groups
    {
        get;
        init => field = value.IsDefault ? [] : value;
    }

    /// <summary>
    /// Gets the explicit "is-a" relations of the hierarchy in registration order.
    /// </summary>
    /// <remarks>
    /// Endpoints are stored as configured; the registry normalizes aliases to their group's primary MIME
    /// type when it builds its hierarchy lookup. Assigning a default array stores an empty array.
    /// </remarks>
    public required ImmutableArray<(MimeType Child, MimeType Parent)> ParentRelations
    {
        get;
        init => field = value.IsDefault ? [] : value;
    }

    /// <summary>
    /// Gets the implicit parent configured for each structured-syntax suffix, keyed without the leading
    /// <c>+</c>.
    /// </summary>
    /// <remarks>
    /// Keys are case-insensitive and must be unique. These mappings only take effect while
    /// <see cref="StructuredSyntaxSuffixRulesEnabled" /> is <see langword="true" />. Assigning a default
    /// array stores an empty array.
    /// </remarks>
    public required ImmutableArray<KeyValuePair<string, MimeType>> SuffixParents
    {
        get;
        init => field = value.IsDefault ? [] : value;
    }

    /// <summary>
    /// Gets the explicit group preference order configured for ambiguous file extensions.
    /// </summary>
    /// <remarks>
    /// Each entry lists the primary MIME types of the groups that should be returned first when the
    /// extension is looked up; claiming groups not listed follow in registration order. Assigning a
    /// default array stores an empty array.
    /// </remarks>
    public required ImmutableArray<KeyValuePair<FileExtension, ImmutableArray<MimeType>>> ExtensionPreferences
    {
        get;
        init => field = value.IsDefault ? [] : value;
    }

    /// <summary>
    /// Gets the limits used when string-based registry lookups must parse a MIME type unknown to the
    /// registry.
    /// </summary>
    /// <exception cref="ArgumentNullException">The assigned value is <see langword="null" />.</exception>
    public required MimeTypeParseOptions ParseOptions
    {
        get;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets a value indicating whether a type with no explicit parent may derive from the parent
    /// configured for its structured-syntax suffix in <see cref="SuffixParents" />.
    /// </summary>
    public required bool StructuredSyntaxSuffixRulesEnabled { get; init; }

    /// <summary>
    /// Gets a value indicating whether a <c>text/*</c> type with no explicit parent derives from
    /// <see cref="TextParent" />.
    /// </summary>
    public required bool TextPlainRuleEnabled { get; init; }

    /// <summary>
    /// Gets a value indicating whether types not handled by an explicit, suffix, or text rule derive
    /// from <see cref="FallbackParent" />.
    /// </summary>
    public required bool FallbackRuleEnabled { get; init; }

    /// <summary>
    /// Gets the implicit parent for <c>text/*</c> MIME types other than the parent itself.
    /// </summary>
    /// <remarks>This value has no effect while <see cref="TextPlainRuleEnabled" /> is <see langword="false" />.</remarks>
    public required MimeType TextParent { get; init; }

    /// <summary>
    /// Gets the ultimate implicit parent for otherwise unmatched MIME types.
    /// </summary>
    /// <remarks>This value has no effect while <see cref="FallbackRuleEnabled" /> is <see langword="false" />.</remarks>
    public required MimeType FallbackParent { get; init; }

    /// <summary>
    /// Validates this configuration and returns a message for every violation found.
    /// </summary>
    /// <returns>
    /// A list of human-readable violation messages; empty when the configuration is valid.
    /// </returns>
    /// <remarks>
    /// The <see cref="MimeTypeRegistry(MimeTypeRegistryConfiguration)" /> constructor calls this method
    /// and throws a <see cref="MimeTypeRegistryValidationException" /> listing every returned message.
    /// </remarks>
    public List<string> GetViolations()
    {
        var violations = new List<string>();
        var membership = new Dictionary<MimeType, int>();
        var primaryGroups = new Dictionary<MimeType, MimeTypeGroup>();

        for (var groupIndex = 0; groupIndex < Groups.Length; groupIndex++)
        {
            var group = Groups[groupIndex];
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract -- hand-assembled arrays can contain null despite the annotation
            if (group is null)
            {
                violations.Add($"Group {groupIndex + 1} is null.");
                continue;
            }

            var localMembers = new HashSet<MimeType>();
            ValidateMember(group.PrimaryMimeType, "primary", groupIndex, localMembers, membership, violations);
            primaryGroups.TryAdd(group.PrimaryMimeType, group);

            foreach (var alias in group.Aliases)
            {
                ValidateMember(alias, "alias", groupIndex, localMembers, membership, violations);
            }

            var localExtensions = new HashSet<FileExtension>();
            foreach (var extension in group.Extensions)
            {
                if (extension.IsDefault)
                {
                    violations.Add($"Group {groupIndex + 1} contains an invalid default file extension.");
                }
                else if (!localExtensions.Add(extension))
                {
                    violations.Add($"Group '{group.PrimaryMimeType}' contains duplicate extension '{extension}'.");
                }
            }
        }

        foreach (var relation in ParentRelations)
        {
            if (relation.Child.IsDefault || relation.Parent.IsDefault)
            {
                violations.Add("An explicit parent relation contains a default MIME type.");
            }
        }

        if (TextParent.IsDefault)
        {
            violations.Add("The text rule parent is a default MIME type.");
        }

        if (FallbackParent.IsDefault)
        {
            violations.Add("The fallback rule parent is a default MIME type.");
        }

        ValidateSuffixParents(violations);
        ValidateExtensionPreferences(primaryGroups, violations);
        ValidateExplicitCycles(membership, violations);
        return violations;
    }

    private void ValidateSuffixParents(List<string> violations)
    {
        var seenSuffixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var suffixParent in SuffixParents)
        {
            if (!MimeType.IsValidSuffix(suffixParent.Key))
            {
                violations.Add($"'{suffixParent.Key}' is not a valid structured-syntax suffix.");
            }
            else if (!seenSuffixes.Add(suffixParent.Key))
            {
                violations.Add($"The '+{suffixParent.Key}' suffix is configured more than once.");
            }

            if (suffixParent.Value.IsDefault)
            {
                violations.Add($"The '+{suffixParent.Key}' suffix parent is a default MIME type.");
            }
        }
    }

    private void ValidateExtensionPreferences(
        Dictionary<MimeType, MimeTypeGroup> primaryGroups,
        List<string> violations
    )
    {
        var seenExtensions = new HashSet<FileExtension>();
        foreach (var preference in ExtensionPreferences)
        {
            if (preference.Key.IsDefault)
            {
                violations.Add("An extension preference uses a default file extension.");
                continue;
            }

            if (!seenExtensions.Add(preference.Key))
            {
                violations.Add($"The preference for '{preference.Key}' is configured more than once.");
            }

            if (preference.Value.IsDefault)
            {
                violations.Add($"The preference for '{preference.Key}' has no MIME type list.");
                continue;
            }

            var seen = new HashSet<MimeType>();
            foreach (var primary in preference.Value)
            {
                if (!seen.Add(primary))
                {
                    violations.Add($"Preference for '{preference.Key}' repeats group '{primary}'.");
                }

                if (!primaryGroups.TryGetValue(primary, out var group) || !group.Extensions.Contains(preference.Key))
                {
                    violations.Add(
                        $"Preference for '{preference.Key}' references non-claiming group '{primary}'."
                    );
                }
            }
        }
    }

    private void ValidateExplicitCycles(Dictionary<MimeType, int> membership, List<string> violations)
    {
        var parentsByChild = new Dictionary<MimeType, HashSet<MimeType>>();
        foreach (var relation in ParentRelations)
        {
            if (relation.Child.IsDefault || relation.Parent.IsDefault)
            {
                continue;
            }

            var child = NormalizeMember(relation.Child, membership);
            var parent = NormalizeMember(relation.Parent, membership);
            if (!parentsByChild.TryGetValue(child, out var parents))
            {
                parents = [];
                parentsByChild.Add(child, parents);
            }

            parents.Add(parent);
        }

        var states = new Dictionary<MimeType, VisitState>();
        var reportedCycles = new HashSet<MimeType>();
        foreach (var child in parentsByChild.Keys)
        {
            Visit(child, parentsByChild, states, reportedCycles, violations);
        }
    }

    private MimeType NormalizeMember(MimeType value, Dictionary<MimeType, int> membership) =>
        membership.TryGetValue(value, out var groupIndex) ? Groups[groupIndex].PrimaryMimeType : value;

    private static void Visit(
        MimeType child,
        Dictionary<MimeType, HashSet<MimeType>> parentsByChild,
        Dictionary<MimeType, VisitState> states,
        HashSet<MimeType> reportedCycles,
        List<string> violations
    )
    {
        if (states.TryGetValue(child, out var state) && state == VisitState.Completed)
        {
            return;
        }

        states[child] = VisitState.InProgress;
        if (parentsByChild.TryGetValue(child, out var parents))
        {
            foreach (var parent in parents)
            {
                if (states.TryGetValue(parent, out var parentState) && parentState == VisitState.InProgress)
                {
                    if (reportedCycles.Add(parent))
                    {
                        violations.Add($"The explicit hierarchy contains a cycle involving '{parent}'.");
                    }
                }
                else
                {
                    Visit(parent, parentsByChild, states, reportedCycles, violations);
                }
            }
        }

        states[child] = VisitState.Completed;
    }

    private static void ValidateMember(
        MimeType member,
        string role,
        int groupIndex,
        HashSet<MimeType> localMembers,
        Dictionary<MimeType, int> membership,
        List<string> violations
    )
    {
        if (member.IsDefault)
        {
            violations.Add($"Group {groupIndex + 1} contains an invalid default {role} MIME type.");
            return;
        }

        if (!localMembers.Add(member))
        {
            violations.Add($"Group {groupIndex + 1} contains duplicate MIME type '{member}'.");
            return;
        }

        if (membership.TryGetValue(member, out var existingGroupIndex))
        {
            violations.Add(
                $"MIME type '{member}' belongs to groups {existingGroupIndex + 1} and {groupIndex + 1}."
            );
        }
        else
        {
            membership.Add(member, groupIndex);
        }
    }

    private enum VisitState
    {
        InProgress,
        Completed
    }
}
