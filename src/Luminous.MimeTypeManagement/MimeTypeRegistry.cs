using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Luminous.MimeTypeManagement;

/// <summary>
/// Provides immutable, thread-safe MIME type normalization, extension lookup, and hierarchy queries.
/// </summary>
/// <remarks>
/// <para>
/// Normalization and hierarchy answer different questions. Use <see cref="Normalize(MimeType)" /> to
/// collapse alternate names for the same format to a canonical MIME type. Use
/// <see cref="IsSubtypeOf(MimeType, MimeType)" /> to test format compatibility or containment without
/// changing identity, such as determining that a DOCX format is based on ZIP.
/// </para>
/// <para>
/// Registries are snapshots and may be shared across threads. Create one with
/// <see cref="MimeTypeRegistryBuilder" /> or directly from a
/// <see cref="MimeTypeRegistryConfiguration" />; call <see cref="ToBuilder" /> when a configured registry
/// must be customized without changing existing readers.
/// </para>
/// </remarks>
public sealed class MimeTypeRegistry
{
    private readonly MimeTypeRegistryConfiguration _configuration;
    private readonly MimeType _fallbackParent;
    private readonly FrozenDictionary<FileExtension, ImmutableArray<MimeTypeGroup>> _groupsByExtension;
    private readonly FrozenDictionary<MimeType, MimeTypeGroup> _groupsByMimeType;
    private readonly FrozenDictionary<string, MimeType>.AlternateLookup<ReadOnlySpan<char>> _knownMimeTypesBySpan;
    private readonly FrozenDictionary<MimeType, ImmutableArray<MimeType>> _parentsByMimeType;
    private readonly FrozenDictionary<string, MimeType> _suffixParents;
    private readonly MimeType _textParent;

    /// <summary>
    /// Validates a configuration and creates an immutable, thread-safe registry from it.
    /// </summary>
    /// <param name="configuration">The complete registry configuration.</param>
    /// <exception cref="ArgumentNullException"><paramref name="configuration" /> is <see langword="null" />.</exception>
    /// <exception cref="MimeTypeRegistryValidationException">
    /// <paramref name="configuration" /> contains one or more invalid entries, such as duplicate group
    /// membership or a cycle in the explicit hierarchy. The exception lists all detected violations.
    /// </exception>
    /// <remarks>
    /// <see cref="MimeTypeRegistryBuilder.Build" /> calls this constructor after taking a snapshot of the
    /// builder's state. Call it directly when a configuration is assembled without a builder, for example
    /// after deserializing configuration data.
    /// </remarks>
    public MimeTypeRegistry(MimeTypeRegistryConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var violations = configuration.GetViolations();
        if (violations.Count > 0)
        {
            throw new MimeTypeRegistryValidationException(violations);
        }

        _configuration = configuration;

        var groupsByMimeType = new Dictionary<MimeType, MimeTypeGroup>();
        foreach (var group in configuration.Groups)
        {
            groupsByMimeType.Add(group.PrimaryMimeType, group);
            foreach (var alias in group.Aliases)
            {
                groupsByMimeType.Add(alias, group);
            }
        }

        _groupsByMimeType = groupsByMimeType.ToFrozenDictionary();
        _textParent = NormalizeCore(configuration.TextParent, groupsByMimeType);
        _fallbackParent = NormalizeCore(configuration.FallbackParent, groupsByMimeType);

        var normalizedSuffixParents = new Dictionary<string, MimeType>(StringComparer.OrdinalIgnoreCase);
        foreach (var suffixParent in configuration.SuffixParents)
        {
            normalizedSuffixParents.Add(
                suffixParent.Key,
                NormalizeCore(suffixParent.Value, groupsByMimeType)
            );
        }

        _suffixParents = normalizedSuffixParents.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        _parentsByMimeType = CreateParentLookup(configuration.ParentRelations, groupsByMimeType);
        _groupsByExtension = CreateExtensionLookup(configuration.Groups, configuration.ExtensionPreferences);
        var knownMimeTypes = CreateKnownMimeTypeLookup(configuration);
        _knownMimeTypesBySpan = knownMimeTypes.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    /// <summary>
    /// Gets the equivalence groups in registration order.
    /// </summary>
    public ImmutableArray<MimeTypeGroup> Groups => _configuration.Groups;

    /// <summary>
    /// Gets the limits used when string-based lookups must parse a MIME type unknown to the registry.
    /// </summary>
    /// <remarks>Known registry values are resolved directly and do not require fallback parsing.</remarks>
    public MimeTypeParseOptions ParseOptions => _configuration.ParseOptions;

    /// <summary>
    /// Tries to find the equivalence group containing a MIME type as either its primary value or an alias.
    /// </summary>
    /// <param name="mimeType">The parsed MIME type to find.</param>
    /// <param name="group">The matching group when found; otherwise, <see langword="null" />.</param>
    /// <returns><see langword="true" /> when the MIME type is a registered group member.</returns>
    public bool TryGetGroup(MimeType mimeType, [NotNullWhen(true)] out MimeTypeGroup? group)
    {
        if (mimeType.IsDefault)
        {
            group = null;
            return false;
        }

        return _groupsByMimeType.TryGetValue(mimeType, out group);
    }

    /// <summary>
    /// Tries to parse a MIME type and find the group containing it as a primary value or alias.
    /// </summary>
    /// <param name="mimeType">
    /// The MIME type name to find. Casing and parameters do not affect lookup.
    /// </param>
    /// <param name="group">The matching group when found; otherwise, <see langword="null" />.</param>
    /// <returns>
    /// <see langword="true" /> when the value is valid and registered; <see langword="false" /> for invalid
    /// or unregistered values.
    /// </returns>
    public bool TryGetGroup(
        ReadOnlySpan<char> mimeType,
        [NotNullWhen(true)] out MimeTypeGroup? group
    )
    {
        if (!TryResolveMimeType(mimeType, out var parsed))
        {
            group = null;
            return false;
        }

        return _groupsByMimeType.TryGetValue(parsed, out group);
    }

    /// <summary>
    /// Replaces a registered alias with its group's primary MIME type.
    /// </summary>
    /// <param name="mimeType">The MIME type to normalize.</param>
    /// <returns>
    /// The group's primary MIME type when registered; otherwise, <paramref name="mimeType" /> unchanged.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="mimeType" /> is the default value.</exception>
    public MimeType Normalize(MimeType mimeType)
    {
        if (mimeType.IsDefault)
        {
            throw new ArgumentException("A default MIME type cannot be normalized.", nameof(mimeType));
        }

        return _groupsByMimeType.TryGetValue(mimeType, out var group) ? group.PrimaryMimeType : mimeType;
    }

    /// <summary>
    /// Parses a MIME type and replaces a registered alias with its group's primary MIME type.
    /// </summary>
    /// <param name="mimeType">
    /// The MIME type name to normalize. Casing is normalized and parameters are discarded.
    /// </param>
    /// <returns>
    /// The group's primary MIME type when registered; otherwise, the parsed unknown MIME type. This
    /// pass-through behavior allows valid vendor-specific values to survive normalization.
    /// </returns>
    /// <exception cref="FormatException"><paramref name="mimeType" /> is not a valid MIME type name.</exception>
    public MimeType Normalize(ReadOnlySpan<char> mimeType)
    {
        if (!TryResolveMimeType(mimeType, out var parsed))
        {
            throw new FormatException(MimeType.InvalidMediaTypeNameMessage);
        }

        return Normalize(parsed);
    }

    /// <summary>
    /// Tries to normalize a registered primary or alias member to its group's primary MIME type.
    /// </summary>
    /// <param name="mimeType">The MIME type to normalize.</param>
    /// <param name="normalizedMimeType">
    /// The group's primary MIME type when registered; otherwise, the original value.
    /// </param>
    /// <returns>
    /// <see langword="true" /> only when <paramref name="mimeType" /> belongs to a registered group.
    /// </returns>
    /// <remarks>
    /// Unlike <see cref="Normalize(MimeType)" />, a valid but unknown value produces
    /// <see langword="false" /> so callers can distinguish pass-through from registry-backed normalization.
    /// </remarks>
    public bool TryNormalize(MimeType mimeType, out MimeType normalizedMimeType)
    {
        normalizedMimeType = mimeType;
        if (mimeType.IsDefault || !_groupsByMimeType.TryGetValue(mimeType, out var group))
        {
            return false;
        }

        normalizedMimeType = group.PrimaryMimeType;
        return true;
    }

    /// <summary>
    /// Tries to parse and normalize a registered primary or alias member.
    /// </summary>
    /// <param name="mimeType">
    /// The MIME type name to normalize. Casing and parameters do not affect lookup.
    /// </param>
    /// <param name="normalizedMimeType">
    /// The group's primary MIME type when registered; the parsed value when valid but unknown; otherwise,
    /// the default value.
    /// </param>
    /// <returns>
    /// <see langword="true" /> only when <paramref name="mimeType" /> is valid and belongs to a registered group.
    /// </returns>
    public bool TryNormalize(ReadOnlySpan<char> mimeType, out MimeType normalizedMimeType)
    {
        if (!TryResolveMimeType(mimeType, out var parsed))
        {
            normalizedMimeType = default;
            return false;
        }

        return TryNormalize(parsed, out normalizedMimeType);
    }

    /// <summary>
    /// Gets every group claiming an extension, in configured preference order.
    /// </summary>
    /// <param name="extension">The normalized extension to look up.</param>
    /// <returns>An empty array when no group claims the extension.</returns>
    /// <remarks>
    /// Multiple results are expected for ambiguous extensions. Use <see cref="TryGetPreferredGroup(FileExtension, out MimeTypeGroup)" />
    /// only when the first configured candidate is sufficient.
    /// </remarks>
    public ImmutableArray<MimeTypeGroup> GetGroups(FileExtension extension)
    {
        return _groupsByExtension.GetValueOrDefault(extension, []);
    }

    /// <summary>
    /// Parses an extension and gets every claiming group in configured preference order.
    /// </summary>
    /// <param name="extension">An extension in forms such as <c>pdf</c>, <c>.pdf</c>, or <c>*.pdf</c>.</param>
    /// <returns>An empty array when no group claims the valid extension.</returns>
    /// <exception cref="FormatException"><paramref name="extension" /> is not a valid file extension.</exception>
    public ImmutableArray<MimeTypeGroup> GetGroups(ReadOnlySpan<char> extension)
    {
        return GetGroups(FileExtension.Parse(extension));
    }

    /// <summary>
    /// Tries to get every group claiming an extension, in configured preference order.
    /// </summary>
    /// <param name="extension">The normalized extension to look up.</param>
    /// <param name="groups">The ordered claiming groups when found; otherwise, a default immutable array.</param>
    /// <returns><see langword="true" /> when at least one group claims the extension.</returns>
    public bool TryGetGroups(FileExtension extension, out ImmutableArray<MimeTypeGroup> groups)
    {
        return _groupsByExtension.TryGetValue(extension, out groups);
    }

    /// <summary>
    /// Tries to get the highest-priority group claiming an extension.
    /// </summary>
    /// <param name="extension">The normalized extension to look up.</param>
    /// <param name="group">The first group in configured preference order; otherwise, <see langword="null" />.</param>
    /// <returns><see langword="true" /> when at least one group claims the extension.</returns>
    /// <remarks>
    /// An extension alone cannot reliably identify file content. When ambiguity matters, inspect all
    /// candidates from <see cref="GetGroups(FileExtension)" /> and combine them with other evidence.
    /// </remarks>
    public bool TryGetPreferredGroup(
        FileExtension extension,
        [NotNullWhen(true)] out MimeTypeGroup? group
    )
    {
        if (_groupsByExtension.TryGetValue(extension, out var groups) && !groups.IsEmpty)
        {
            group = groups[0];
            return true;
        }

        group = null;
        return false;
    }

    /// <summary>
    /// Tries to parse an extension and get its highest-priority claiming group.
    /// </summary>
    /// <param name="extension">An extension in forms such as <c>pdf</c>, <c>.pdf</c>, or <c>*.pdf</c>.</param>
    /// <param name="group">The first group in configured preference order; otherwise, <see langword="null" />.</param>
    /// <returns>
    /// <see langword="true" /> when the extension is valid and claimed; otherwise, <see langword="false" />.
    /// </returns>
    public bool TryGetPreferredGroup(
        ReadOnlySpan<char> extension,
        [NotNullWhen(true)] out MimeTypeGroup? group
    )
    {
        if (FileExtension.TryParse(extension, out var parsed))
        {
            return TryGetPreferredGroup(parsed, out group);
        }

        group = null;
        return false;
    }

    /// <summary>
    /// Determines whether one MIME type is equal to, or transitively derives from, another.
    /// </summary>
    /// <param name="mimeType">The potential child MIME type.</param>
    /// <param name="potentialParent">The type against which to test compatibility.</param>
    /// <returns>
    /// <see langword="true" /> when the normalized values are equal or a path exists through the configured
    /// explicit and implicit hierarchy; otherwise, <see langword="false" />.
    /// </returns>
    /// <remarks>
    /// Both arguments are alias-normalized first. The relation is reflexive, making it suitable for
    /// capability checks such as "can this handler accept this type?" Explicit parents take precedence:
    /// when a type has any explicit parent, implicit suffix, text, and fallback rules are not applied to
    /// that type itself, though they may apply later while traversing its parents.
    /// </remarks>
    public bool IsSubtypeOf(MimeType mimeType, MimeType potentialParent)
    {
        if (mimeType.IsDefault || potentialParent.IsDefault)
        {
            return false;
        }

        var child = Normalize(mimeType);
        var parent = Normalize(potentialParent);
        if (child == parent)
        {
            return true;
        }

        var visited = new HashSet<MimeType>();
        var pending = new Stack<MimeType>();
        pending.Push(child);

        while (pending.TryPop(out var current))
        {
            if (!visited.Add(current))
            {
                continue;
            }

            if (_parentsByMimeType.TryGetValue(current, out var explicitParents))
            {
                foreach (var explicitParent in explicitParents)
                {
                    if (explicitParent == parent)
                    {
                        return true;
                    }

                    pending.Push(explicitParent);
                }

                continue;
            }

            if (TryGetImplicitParent(current, out var implicitParent))
            {
                if (implicitParent == parent)
                {
                    return true;
                }

                pending.Push(implicitParent);
            }
        }

        return false;
    }

    /// <summary>
    /// Parses two MIME type names and determines whether the first is equal to, or transitively derives
    /// from, the second.
    /// </summary>
    /// <param name="mimeType">The potential child MIME type name.</param>
    /// <param name="potentialParent">The potential parent MIME type name.</param>
    /// <returns>
    /// <see langword="false" /> when either value is invalid; otherwise, the result of the normalized
    /// hierarchy query.
    /// </returns>
    public bool IsSubtypeOf(ReadOnlySpan<char> mimeType, ReadOnlySpan<char> potentialParent)
    {
        return TryResolveMimeType(mimeType, out var child) &&
               TryResolveMimeType(potentialParent, out var parent) &&
               IsSubtypeOf(child, parent);
    }

    /// <summary>
    /// Creates an independent mutable builder containing the registry's complete configuration.
    /// </summary>
    /// <returns>A builder that can remove, replace, or extend groups, hierarchy rules, and preferences.</returns>
    /// <remarks>
    /// Building or modifying the returned builder does not affect this registry. This is the recommended
    /// way to customize a shared or preconfigured registry while existing readers continue using the
    /// original immutable snapshot.
    /// </remarks>
    public MimeTypeRegistryBuilder ToBuilder()
    {
        var builder = new MimeTypeRegistryBuilder(ParseOptions)
        {
            StructuredSyntaxSuffixRulesEnabled = _configuration.StructuredSyntaxSuffixRulesEnabled,
            TextPlainRuleEnabled = _configuration.TextPlainRuleEnabled,
            FallbackRuleEnabled = _configuration.FallbackRuleEnabled,
            TextParent = _configuration.TextParent,
            FallbackParent = _configuration.FallbackParent
        };

        builder.ClearSuffixParents();
        foreach (var suffixParent in _configuration.SuffixParents)
        {
            builder.SetSuffixParent(suffixParent.Key, suffixParent.Value);
        }

        foreach (var group in Groups)
        {
            builder.AddGroup(group);
        }

        foreach (var relation in _configuration.ParentRelations)
        {
            builder.AddParent(relation.Child, relation.Parent);
        }

        foreach (var preference in _configuration.ExtensionPreferences)
        {
            builder.SetExtensionPreference(preference.Key, preference.Value);
        }

        return builder;
    }

    private bool TryResolveMimeType(ReadOnlySpan<char> value, out MimeType mimeType)
    {
        var lookupValue = MimeType.TrimMediaType(value);
        return _knownMimeTypesBySpan.TryGetValue(lookupValue, out mimeType) ||
               MimeType.TryParse(value, ParseOptions, out mimeType);
    }

    private bool TryGetImplicitParent(MimeType mimeType, out MimeType parent)
    {
        if (_configuration.StructuredSyntaxSuffixRulesEnabled &&
            mimeType.Suffix is not null &&
            _suffixParents.TryGetValue(mimeType.Suffix, out parent) &&
            parent != mimeType)
        {
            return true;
        }

        if (_configuration.TextPlainRuleEnabled &&
            mimeType.TopLevelType == "text" &&
            _textParent != mimeType)
        {
            parent = _textParent;
            return true;
        }

        if (_configuration.FallbackRuleEnabled && _fallbackParent != mimeType)
        {
            parent = _fallbackParent;
            return true;
        }

        parent = default;
        return false;
    }

    private static MimeType NormalizeCore(
        MimeType mimeType,
        Dictionary<MimeType, MimeTypeGroup> groupsByMimeType
    )
    {
        return groupsByMimeType.TryGetValue(mimeType, out var group) ? group.PrimaryMimeType : mimeType;
    }

    private static FrozenDictionary<MimeType, ImmutableArray<MimeType>> CreateParentLookup(
        ImmutableArray<(MimeType Child, MimeType Parent)> parentRelations,
        Dictionary<MimeType, MimeTypeGroup> groupsByMimeType
    )
    {
        var mutableLookup = new Dictionary<MimeType, HashSet<MimeType>>();
        foreach (var relation in parentRelations)
        {
            var child = NormalizeCore(relation.Child, groupsByMimeType);
            var parent = NormalizeCore(relation.Parent, groupsByMimeType);
            if (!mutableLookup.TryGetValue(child, out var parents))
            {
                parents = [];
                mutableLookup.Add(child, parents);
            }

            parents.Add(parent);
        }

        return mutableLookup.ToFrozenDictionary(
            pair => pair.Key,
            pair => pair.Value.ToImmutableArray()
        );
    }

    private static FrozenDictionary<FileExtension, ImmutableArray<MimeTypeGroup>> CreateExtensionLookup(
        ImmutableArray<MimeTypeGroup> groups,
        ImmutableArray<KeyValuePair<FileExtension, ImmutableArray<MimeType>>> extensionPreferences
    )
    {
        var mutableLookup = new Dictionary<FileExtension, List<MimeTypeGroup>>();
        foreach (var group in groups)
        {
            foreach (var extension in group.Extensions)
            {
                if (!mutableLookup.TryGetValue(extension, out var claimingGroups))
                {
                    claimingGroups = [];
                    mutableLookup.Add(extension, claimingGroups);
                }

                claimingGroups.Add(group);
            }
        }

        var preferences = extensionPreferences.ToDictionary(pair => pair.Key, pair => pair.Value);
        return mutableLookup.ToFrozenDictionary(
            pair => pair.Key,
            pair => OrderGroups(pair.Key, pair.Value, preferences)
        );
    }

    private static ImmutableArray<MimeTypeGroup> OrderGroups(
        FileExtension extension,
        List<MimeTypeGroup> groups,
        Dictionary<FileExtension, ImmutableArray<MimeType>> preferences
    )
    {
        if (!preferences.TryGetValue(extension, out var preference))
        {
            return [..groups];
        }

        var ordered = ImmutableArray.CreateBuilder<MimeTypeGroup>(groups.Count);
        foreach (var primary in preference)
        {
            ordered.Add(groups.Single(group => group.PrimaryMimeType == primary));
        }

        foreach (var group in groups)
        {
            if (!preference.Contains(group.PrimaryMimeType))
            {
                ordered.Add(group);
            }
        }

        return ordered.MoveToImmutable();
    }

    private static FrozenDictionary<string, MimeType> CreateKnownMimeTypeLookup(
        MimeTypeRegistryConfiguration configuration
    )
    {
        var knownMimeTypes = new Dictionary<string, MimeType>(StringComparer.OrdinalIgnoreCase);

        void Add(MimeType mimeType)
        {
            knownMimeTypes.TryAdd(mimeType.Value, mimeType);
        }

        foreach (var group in configuration.Groups)
        {
            Add(group.PrimaryMimeType);
            foreach (var alias in group.Aliases)
            {
                Add(alias);
            }
        }

        foreach (var relation in configuration.ParentRelations)
        {
            Add(relation.Child);
            Add(relation.Parent);
        }

        foreach (var suffixParent in configuration.SuffixParents)
        {
            Add(suffixParent.Value);
        }

        Add(configuration.TextParent);
        Add(configuration.FallbackParent);
        return knownMimeTypes.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }
}
