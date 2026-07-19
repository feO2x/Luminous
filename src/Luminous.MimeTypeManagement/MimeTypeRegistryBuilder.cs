using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Luminous.MimeTypeManagement;

/// <summary>
/// Collects and validates the groups, extension preferences, and hierarchy rules used to create a
/// <see cref="MimeTypeRegistry"/>.
/// </summary>
/// <remarks>
/// <para>
/// The builder is mutable and not thread-safe. <see cref="Build"/> creates an immutable snapshot, so
/// later builder changes do not affect registries that have already been built.
/// </para>
/// <para>
/// Use groups only for names that mean the same format: choose the value your application should store
/// as the primary and register values reported by browsers or operating systems as aliases. Use parent
/// relations for compatibility or containment instead. For example, DOCX can have a ZIP parent, but it
/// should not normalize to <c>application/zip</c>.
/// </para>
/// <para>
/// Validation is intentionally deferred to <see cref="Build"/>, which reports all detected problems in
/// one <see cref="MimeTypeRegistryValidationException"/>. This makes it practical to assemble a registry
/// from several data sources before resolving conflicts.
/// </para>
/// <example>
/// <code>
/// var registry = new MimeTypeRegistryBuilder()
///     .AddGroup("application/zip", ["application/x-zip-compressed"], ["zip"])
///     .AddGroup("application/vnd.openxmlformats-officedocument.wordprocessingml.document",
///         extensions: ["docx"])
///     .AddParent(
///         "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
///         "application/zip")
///     .Build();
///
/// registry.Normalize("application/x-zip-compressed"); // application/zip
/// </code>
/// </example>
/// </remarks>
public sealed class MimeTypeRegistryBuilder
{
    private readonly Dictionary<FileExtension, List<MimeType>> _extensionPreferences = [];
    private readonly List<MimeTypeGroup> _groups = [];
    private readonly List<(MimeType Child, MimeType Parent)> _parentRelations = [];
    private readonly Dictionary<string, MimeType> _suffixParents = new (StringComparer.OrdinalIgnoreCase);
    private MimeTypeParseOptions _parseOptions;

    /// <summary>
    /// Creates an empty builder with the standard implicit hierarchy rules.
    /// </summary>
    /// <param name="parseOptions">
    /// Parsing limits for string configuration and unknown registry input, or <see langword="null"/> to
    /// use <see cref="MimeTypeParseOptions.Default"/>.
    /// </param>
    /// <remarks>
    /// The defaults map <c>+xml</c>, <c>+json</c>, and <c>+zip</c> to their corresponding application
    /// types; map other <c>text/*</c> types to <c>text/plain</c>; and ultimately fall back to
    /// <c>application/octet-stream</c>. No equivalence groups or extensions are added automatically.
    /// </remarks>
    public MimeTypeRegistryBuilder(MimeTypeParseOptions? parseOptions = null)
    {
        _parseOptions = parseOptions ?? MimeTypeParseOptions.Default;
        _suffixParents.Add("xml", MimeType.Parse("application/xml"));
        _suffixParents.Add("json", MimeType.Parse("application/json"));
        _suffixParents.Add("zip", MimeType.Parse("application/zip"));
        TextParent = MimeType.Parse("text/plain");
        FallbackParent = MimeType.Parse("application/octet-stream");
    }

    /// <summary>
    /// Gets or sets the limits used by string-based builder methods and fallback parsing in built registries.
    /// </summary>
    /// <exception cref="ArgumentNullException">The assigned value is <see langword="null"/>.</exception>
    public MimeTypeParseOptions ParseOptions
    {
        get => _parseOptions;
        set => _parseOptions = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets or sets whether a type with no explicit parent may derive from the parent configured for its
    /// final structured-syntax suffix.
    /// </summary>
    /// <remarks>
    /// Enabled by default. Configure mappings with <see cref="SetSuffixParent(string, MimeType)"/>.
    /// </remarks>
    public bool StructuredSyntaxSuffixRulesEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether a <c>text/*</c> type with no explicit parent derives from <see cref="TextParent"/>.
    /// </summary>
    /// <remarks>Enabled by default; the initial parent is <c>text/plain</c>.</remarks>
    public bool TextPlainRuleEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether types not handled by an explicit, suffix, or text rule derive from
    /// <see cref="FallbackParent"/>.
    /// </summary>
    /// <remarks>Enabled by default; the initial fallback is <c>application/octet-stream</c>.</remarks>
    public bool FallbackRuleEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the implicit parent for <c>text/*</c> MIME types other than the normalized parent itself.
    /// </summary>
    /// <remarks>This value has no effect while <see cref="TextPlainRuleEnabled"/> is disabled.</remarks>
    public MimeType TextParent { get; set; }

    /// <summary>
    /// Gets or sets the ultimate implicit parent for otherwise unmatched MIME types.
    /// </summary>
    /// <remarks>This value has no effect while <see cref="FallbackRuleEnabled"/> is disabled.</remarks>
    public MimeType FallbackParent { get; set; }

    /// <summary>
    /// Gets the currently configured equivalence groups in registration order.
    /// </summary>
    /// <remarks>The returned view reflects subsequent additions, removals, and replacements on this builder.</remarks>
    public IReadOnlyList<MimeTypeGroup> Groups => _groups;

    /// <summary>
    /// Adds an equivalence group to the end of the registry configuration.
    /// </summary>
    /// <param name="group">The canonical MIME type, aliases, and ordered extensions for one format.</param>
    /// <returns>This builder, for fluent configuration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="group"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Cross-group membership and duplicate checks occur at <see cref="Build"/>. Registration order is
    /// also the default preference order when several groups claim the same extension.
    /// </remarks>
    public MimeTypeRegistryBuilder AddGroup(MimeTypeGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        _groups.Add(group);
        return this;
    }

    /// <summary>
    /// Creates and adds an equivalence group from parsed values.
    /// </summary>
    /// <param name="primaryMimeType">The canonical MIME type returned by normalization.</param>
    /// <param name="aliases">Alternate names for the same format, or <see langword="null"/> for none.</param>
    /// <param name="extensions">
    /// File extensions in preference order for this format, or <see langword="null"/> for none.
    /// </param>
    /// <returns>This builder, for fluent configuration.</returns>
    /// <remarks>
    /// Aliases must describe the same format. Use <see cref="AddParent(MimeType, MimeType)"/> for broader
    /// compatibility or container relationships.
    /// </remarks>
    public MimeTypeRegistryBuilder AddGroup(
        MimeType primaryMimeType,
        IEnumerable<MimeType>? aliases = null,
        IEnumerable<FileExtension>? extensions = null
    ) =>
        AddGroup(new MimeTypeGroup(primaryMimeType, aliases, extensions));

    /// <summary>
    /// Parses, creates, and adds an equivalence group.
    /// </summary>
    /// <param name="primaryMimeType">The canonical MIME type returned by normalization.</param>
    /// <param name="aliases">Alternate names for the same format, or <see langword="null"/> for none.</param>
    /// <param name="extensions">
    /// Extensions in forms such as <c>pdf</c>, <c>.pdf</c>, or <c>*.pdf</c>, in format preference order.
    /// </param>
    /// <returns>This builder, for fluent configuration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="primaryMimeType"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">A MIME type or file extension cannot be parsed.</exception>
    /// <remarks>All MIME type names are parsed with <see cref="ParseOptions"/>.</remarks>
    public MimeTypeRegistryBuilder AddGroup(
        string primaryMimeType,
        IEnumerable<string>? aliases = null,
        IEnumerable<string>? extensions = null
    )
    {
        ArgumentNullException.ThrowIfNull(primaryMimeType);
        var parsedAliases = aliases?.Select(alias => MimeType.Parse(alias, ParseOptions));
        var parsedExtensions = extensions?.Select(extension => FileExtension.Parse(extension));
        return AddGroup(MimeType.Parse(primaryMimeType, ParseOptions), parsedAliases, parsedExtensions);
    }

    /// <summary>
    /// Removes every group whose primary MIME type equals the supplied value.
    /// </summary>
    /// <param name="primaryMimeType">The primary MIME type identifying the group; aliases do not match.</param>
    /// <returns><see langword="true"/> when at least one group was removed.</returns>
    /// <remarks>
    /// Parent relations and extension preferences are not removed automatically. Update those separately
    /// when they should not survive the group removal.
    /// </remarks>
    public bool RemoveGroup(MimeType primaryMimeType) =>
        _groups.RemoveAll(group => group.PrimaryMimeType == primaryMimeType) > 0;

    /// <summary>
    /// Parses a primary MIME type and removes its group.
    /// </summary>
    /// <param name="primaryMimeType">The primary MIME type identifying the group; aliases do not match.</param>
    /// <returns>
    /// <see langword="true"/> when a matching group was removed; <see langword="false"/> when the value is
    /// invalid or no primary matches.
    /// </returns>
    public bool RemoveGroup(ReadOnlySpan<char> primaryMimeType) =>
        MimeType.TryParse(primaryMimeType, ParseOptions, out var parsed) && RemoveGroup(parsed);

    /// <summary>
    /// Replaces the group identified by its current primary MIME type while preserving its registration position.
    /// </summary>
    /// <param name="primaryMimeType">The current primary MIME type; aliases do not identify a group.</param>
    /// <param name="replacement">The complete replacement group, which may have a different primary.</param>
    /// <returns>This builder, for fluent configuration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="replacement"/> is <see langword="null"/>.</exception>
    /// <exception cref="KeyNotFoundException">No group has <paramref name="primaryMimeType"/> as its primary.</exception>
    /// <remarks>
    /// Existing parent relations and extension preferences are left unchanged. If the replacement changes
    /// its primary or extensions, update affected references before calling <see cref="Build"/>.
    /// </remarks>
    public MimeTypeRegistryBuilder ReplaceGroup(MimeType primaryMimeType, MimeTypeGroup replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        var index = _groups.FindIndex(group => group.PrimaryMimeType == primaryMimeType);
        if (index < 0)
        {
            throw new KeyNotFoundException($"No group with primary MIME type '{primaryMimeType}' exists.");
        }

        _groups[index] = replacement;
        return this;
    }

    /// <summary>
    /// Adds an explicit "is-a" relation used by transitive hierarchy queries.
    /// </summary>
    /// <param name="child">The more specific or contained format.</param>
    /// <param name="parent">A broader format or capability that can handle the child.</param>
    /// <returns>This builder, for fluent configuration.</returns>
    /// <remarks>
    /// Duplicate relations are ignored. Group aliases at either endpoint are normalized during build.
    /// A child with any explicit parent does not use an implicit suffix, text, or fallback parent at that
    /// point in traversal; this lets explicit modeling override conventions.
    /// </remarks>
    public MimeTypeRegistryBuilder AddParent(MimeType child, MimeType parent)
    {
        if (!_parentRelations.Contains((child, parent)))
        {
            _parentRelations.Add((child, parent));
        }

        return this;
    }

    /// <summary>
    /// Parses and adds an explicit "is-a" relation used by transitive hierarchy queries.
    /// </summary>
    /// <param name="child">The more specific or contained MIME type name.</param>
    /// <param name="parent">The broader MIME type name.</param>
    /// <returns>This builder, for fluent configuration.</returns>
    /// <exception cref="FormatException">Either MIME type name cannot be parsed with <see cref="ParseOptions"/>.</exception>
    public MimeTypeRegistryBuilder AddParent(ReadOnlySpan<char> child, ReadOnlySpan<char> parent) =>
        AddParent(MimeType.Parse(child, ParseOptions), MimeType.Parse(parent, ParseOptions));

    /// <summary>
    /// Adds an explicit "is-a" relation; this is a named synonym for <see cref="AddParent(MimeType, MimeType)"/>.
    /// </summary>
    /// <param name="child">The more specific or contained format.</param>
    /// <param name="parent">The broader format or capability.</param>
    /// <returns>This builder, for fluent configuration.</returns>
    public MimeTypeRegistryBuilder AddParentRelation(MimeType child, MimeType parent) => AddParent(child, parent);

    /// <summary>
    /// Removes one exact explicit parent relation.
    /// </summary>
    /// <param name="child">The child endpoint used when the relation was added.</param>
    /// <param name="parent">The parent endpoint used when the relation was added.</param>
    /// <returns><see langword="true"/> when the relation existed and was removed.</returns>
    public bool RemoveParent(MimeType child, MimeType parent) => _parentRelations.Remove((child, parent));

    /// <summary>
    /// Removes one exact explicit parent relation; this is a named synonym for
    /// <see cref="RemoveParent(MimeType, MimeType)"/>.
    /// </summary>
    /// <param name="child">The child endpoint used when the relation was added.</param>
    /// <param name="parent">The parent endpoint used when the relation was added.</param>
    /// <returns><see langword="true"/> when the relation existed and was removed.</returns>
    public bool RemoveParentRelation(MimeType child, MimeType parent) => RemoveParent(child, parent);

    /// <summary>
    /// Removes all explicit parent relations configured with the exact supplied child endpoint.
    /// </summary>
    /// <param name="child">The child whose outgoing relations should be removed.</param>
    /// <returns>The number of relations removed.</returns>
    public int ClearParents(MimeType child) => _parentRelations.RemoveAll(relation => relation.Child == child);

    /// <summary>
    /// Adds or replaces the implicit parent for a structured-syntax suffix.
    /// </summary>
    /// <param name="suffix">The final subtype suffix, with or without a leading <c>+</c>.</param>
    /// <param name="parent">The type from which matching MIME types implicitly derive.</param>
    /// <returns>This builder, for fluent configuration.</returns>
    /// <exception cref="ArgumentException"><paramref name="suffix"/> is null, empty, or whitespace.</exception>
    /// <exception cref="FormatException"><paramref name="suffix"/> is not a valid suffix name.</exception>
    /// <remarks>
    /// The mapping applies only while <see cref="StructuredSyntaxSuffixRulesEnabled"/> is enabled and
    /// only to a MIME type with no explicit parent. Suffix keys are case-insensitive.
    /// </remarks>
    public MimeTypeRegistryBuilder SetSuffixParent(string suffix, MimeType parent)
    {
        _suffixParents[NormalizeSuffix(suffix)] = parent;
        return this;
    }

    /// <summary>
    /// Parses a MIME type and adds or replaces the implicit parent for a structured-syntax suffix.
    /// </summary>
    /// <param name="suffix">The final subtype suffix, with or without a leading <c>+</c>.</param>
    /// <param name="parent">The implicit parent MIME type name.</param>
    /// <returns>This builder, for fluent configuration.</returns>
    /// <exception cref="ArgumentException"><paramref name="suffix"/> is null, empty, or whitespace.</exception>
    /// <exception cref="FormatException">The suffix or parent MIME type name is invalid.</exception>
    public MimeTypeRegistryBuilder SetSuffixParent(string suffix, ReadOnlySpan<char> parent) =>
        SetSuffixParent(suffix, MimeType.Parse(parent, ParseOptions));

    /// <summary>
    /// Removes the implicit mapping for a structured-syntax suffix.
    /// </summary>
    /// <param name="suffix">The suffix to remove, with or without a leading <c>+</c>.</param>
    /// <returns><see langword="true"/> when a mapping existed and was removed.</returns>
    /// <exception cref="ArgumentException"><paramref name="suffix"/> is null, empty, or whitespace.</exception>
    /// <exception cref="FormatException"><paramref name="suffix"/> is not a valid suffix name.</exception>
    public bool RemoveSuffixParent(string suffix) => _suffixParents.Remove(NormalizeSuffix(suffix));

    /// <summary>
    /// Removes every structured-syntax suffix mapping, including the default XML, JSON, and ZIP mappings.
    /// </summary>
    /// <remarks>Enabling suffix rules later does not restore cleared mappings.</remarks>
    public void ClearSuffixParents() => _suffixParents.Clear();

    /// <summary>
    /// Sets the preference order for groups that claim an ambiguous file extension.
    /// </summary>
    /// <param name="extension">The shared extension whose lookup order should be changed.</param>
    /// <param name="orderedGroupPrimaries">
    /// Primary MIME types to place first, in descending preference order.
    /// </param>
    /// <returns>This builder, for fluent configuration.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="orderedGroupPrimaries"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// The list may omit claiming groups; omitted groups follow in registration order. Every listed
    /// primary must identify a group that claims <paramref name="extension"/>, and duplicates are invalid.
    /// These conditions are checked by <see cref="Build"/>.
    /// </remarks>
    public MimeTypeRegistryBuilder SetExtensionPreference(
        FileExtension extension,
        IEnumerable<MimeType> orderedGroupPrimaries
    )
    {
        ArgumentNullException.ThrowIfNull(orderedGroupPrimaries);
        _extensionPreferences[extension] = orderedGroupPrimaries.ToList();
        return this;
    }

    /// <summary>
    /// Sets the preference order for groups that claim an ambiguous file extension.
    /// </summary>
    /// <param name="extension">The shared extension whose lookup order should be changed.</param>
    /// <param name="orderedGroupPrimaries">
    /// Primary MIME types to place first, in descending preference order. Unlisted claiming groups follow
    /// in registration order.
    /// </param>
    /// <returns>This builder, for fluent configuration.</returns>
    public MimeTypeRegistryBuilder SetExtensionPreference(
        FileExtension extension,
        params MimeType[] orderedGroupPrimaries
    ) =>
        SetExtensionPreference(extension, (IEnumerable<MimeType>) orderedGroupPrimaries);

    /// <summary>
    /// Removes the explicit preference for an extension, restoring registration order for its claiming groups.
    /// </summary>
    /// <param name="extension">The extension whose override should be removed.</param>
    /// <returns><see langword="true"/> when an override existed and was removed.</returns>
    public bool ClearExtensionPreference(FileExtension extension) => _extensionPreferences.Remove(extension);

    /// <summary>
    /// Validates the complete configuration and creates an immutable, thread-safe registry snapshot.
    /// </summary>
    /// <returns>A registry unaffected by subsequent changes to this builder.</returns>
    /// <exception cref="MimeTypeRegistryValidationException">
    /// One or more MIME types have invalid or duplicate membership, a group repeats an extension, an
    /// extension preference is inconsistent, a configured value is default, or the explicit hierarchy
    /// contains a cycle. The exception contains all violations detected in the validation pass.
    /// </exception>
    public MimeTypeRegistry Build()
    {
        var violations = Validate();
        if (violations.Count > 0)
        {
            throw new MimeTypeRegistryValidationException(violations);
        }

        var parentRelations = _parentRelations.ToImmutableArray();
        var suffixParents = _suffixParents.ToImmutableArray();
        var extensionPreferences = _extensionPreferences
           .Select(
                pair => new KeyValuePair<FileExtension, ImmutableArray<MimeType>>(
                    pair.Key,
                    [..pair.Value]
                )
            )
           .ToImmutableArray();

        return new MimeTypeRegistry(
            [.._groups],
            parentRelations,
            suffixParents,
            extensionPreferences,
            ParseOptions,
            StructuredSyntaxSuffixRulesEnabled,
            TextPlainRuleEnabled,
            FallbackRuleEnabled,
            TextParent,
            FallbackParent
        );
    }

    private List<string> Validate()
    {
        var violations = new List<string>();
        var membership = new Dictionary<MimeType, int>();
        var primaryGroups = new Dictionary<MimeType, MimeTypeGroup>();

        for (var groupIndex = 0; groupIndex < _groups.Count; groupIndex++)
        {
            var group = _groups[groupIndex];
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

        foreach (var relation in _parentRelations)
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

        foreach (var suffixParent in _suffixParents)
        {
            if (suffixParent.Value.IsDefault)
            {
                violations.Add($"The '+{suffixParent.Key}' suffix parent is a default MIME type.");
            }
        }

        ValidateExtensionPreferences(primaryGroups, violations);
        ValidateExplicitCycles(membership, violations);
        return violations;
    }

    private void ValidateExtensionPreferences(
        Dictionary<MimeType, MimeTypeGroup> primaryGroups,
        List<string> violations
    )
    {
        foreach (var preference in _extensionPreferences)
        {
            if (preference.Key.IsDefault)
            {
                violations.Add("An extension preference uses a default file extension.");
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
        foreach (var relation in _parentRelations)
        {
            if (relation.Child.IsDefault || relation.Parent.IsDefault)
            {
                continue;
            }

            var child = Normalize(relation.Child, membership);
            var parent = Normalize(relation.Parent, membership);
            if (!parentsByChild.TryGetValue(child, out var parents))
            {
                parents = [];
                parentsByChild.Add(child, parents);
            }

            parents.Add(parent);
        }

        var states = new Dictionary<MimeType, int>();
        var reportedCycles = new HashSet<MimeType>();
        foreach (var child in parentsByChild.Keys)
        {
            Visit(child, parentsByChild, states, reportedCycles, violations);
        }
    }

    private MimeType Normalize(MimeType value, Dictionary<MimeType, int> membership) =>
        membership.TryGetValue(value, out var groupIndex) ? _groups[groupIndex].PrimaryMimeType : value;

    private static void Visit(
        MimeType child,
        Dictionary<MimeType, HashSet<MimeType>> parentsByChild,
        Dictionary<MimeType, int> states,
        HashSet<MimeType> reportedCycles,
        List<string> violations
    )
    {
        if (states.TryGetValue(child, out var state) && state == 2)
        {
            return;
        }

        states[child] = 1;
        if (parentsByChild.TryGetValue(child, out var parents))
        {
            foreach (var parent in parents)
            {
                if (states.TryGetValue(parent, out var parentState) && parentState == 1)
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

        states[child] = 2;
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

    private static string NormalizeSuffix(string suffix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suffix);
        if (suffix[0] == '+')
        {
            suffix = suffix[1..];
        }

        if (suffix.Length == 0 ||
            !MimeType.TryParse($"application/x+{suffix}", out var mimeType) ||
            !string.Equals(mimeType.Suffix, suffix, StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException("The value is not a valid structured-syntax suffix.");
        }

        return suffix.ToLowerInvariant();
    }
}
