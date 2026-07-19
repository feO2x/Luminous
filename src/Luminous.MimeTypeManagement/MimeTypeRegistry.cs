using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Luminous.MimeTypeManagement;

public sealed class MimeTypeRegistry
{
    private readonly MimeType _fallbackParent;
    private readonly bool _fallbackRuleEnabled;
    private readonly FrozenDictionary<FileExtension, ImmutableArray<MimeTypeGroup>> _groupsByExtension;
    private readonly FrozenDictionary<MimeType, MimeTypeGroup> _groupsByMimeType;
    private readonly FrozenDictionary<string, MimeType> _knownMimeTypes;
    private readonly FrozenDictionary<string, MimeType>.AlternateLookup<ReadOnlySpan<char>> _knownMimeTypesBySpan;
    private readonly MimeType _normalizedTextPlain;
    private readonly FrozenDictionary<MimeType, ImmutableArray<MimeType>> _parentsByMimeType;
    private readonly ImmutableArray<KeyValuePair<FileExtension, ImmutableArray<MimeType>>> _sourceExtensionPreferences;
    private readonly MimeType _sourceFallbackParent;
    private readonly ImmutableArray<(MimeType Child, MimeType Parent)> _sourceParentRelations;
    private readonly ImmutableArray<KeyValuePair<string, MimeType>> _sourceSuffixParents;
    private readonly MimeType _sourceTextParent;
    private readonly bool _structuredSyntaxSuffixRulesEnabled;
    private readonly FrozenDictionary<string, MimeType> _suffixParents;
    private readonly MimeType _textParent;
    private readonly bool _textPlainRuleEnabled;

    public MimeTypeRegistry()
        : this(new MimeTypeRegistryBuilder().Build()) { }

    private MimeTypeRegistry(MimeTypeRegistry source)
    {
        Groups = source.Groups;
        ParseOptions = source.ParseOptions;
        _groupsByMimeType = source._groupsByMimeType;
        _groupsByExtension = source._groupsByExtension;
        _parentsByMimeType = source._parentsByMimeType;
        _knownMimeTypes = source._knownMimeTypes;
        _knownMimeTypesBySpan = _knownMimeTypes.GetAlternateLookup<ReadOnlySpan<char>>();
        _suffixParents = source._suffixParents;
        _sourceParentRelations = source._sourceParentRelations;
        _sourceSuffixParents = source._sourceSuffixParents;
        _sourceExtensionPreferences = source._sourceExtensionPreferences;
        _structuredSyntaxSuffixRulesEnabled = source._structuredSyntaxSuffixRulesEnabled;
        _textPlainRuleEnabled = source._textPlainRuleEnabled;
        _fallbackRuleEnabled = source._fallbackRuleEnabled;
        _sourceTextParent = source._sourceTextParent;
        _sourceFallbackParent = source._sourceFallbackParent;
        _textParent = source._textParent;
        _fallbackParent = source._fallbackParent;
        _normalizedTextPlain = source._normalizedTextPlain;
    }

    public MimeTypeRegistry(MimeTypeRegistryBuilder builder)
        : this((builder ?? throw new ArgumentNullException(nameof(builder))).Build()) { }

    internal MimeTypeRegistry(
        ImmutableArray<MimeTypeGroup> groups,
        ImmutableArray<(MimeType Child, MimeType Parent)> parentRelations,
        ImmutableArray<KeyValuePair<string, MimeType>> suffixParents,
        ImmutableArray<KeyValuePair<FileExtension, ImmutableArray<MimeType>>> extensionPreferences,
        MimeTypeParseOptions parseOptions,
        bool structuredSyntaxSuffixRulesEnabled,
        bool textPlainRuleEnabled,
        bool fallbackRuleEnabled,
        MimeType textParent,
        MimeType fallbackParent
    )
    {
        Groups = groups;
        ParseOptions = parseOptions;
        _sourceParentRelations = parentRelations;
        _sourceSuffixParents = suffixParents;
        _sourceExtensionPreferences = extensionPreferences;
        _structuredSyntaxSuffixRulesEnabled = structuredSyntaxSuffixRulesEnabled;
        _textPlainRuleEnabled = textPlainRuleEnabled;
        _fallbackRuleEnabled = fallbackRuleEnabled;
        _sourceTextParent = textParent;
        _sourceFallbackParent = fallbackParent;

        var groupsByMimeType = new Dictionary<MimeType, MimeTypeGroup>();
        foreach (var group in groups)
        {
            groupsByMimeType.Add(group.PrimaryMimeType, group);
            foreach (var alias in group.Aliases)
            {
                groupsByMimeType.Add(alias, group);
            }
        }

        _groupsByMimeType = groupsByMimeType.ToFrozenDictionary();
        _textParent = NormalizeCore(textParent, groupsByMimeType);
        _fallbackParent = NormalizeCore(fallbackParent, groupsByMimeType);
        _normalizedTextPlain = NormalizeCore(MimeType.Parse("text/plain"), groupsByMimeType);

        var normalizedSuffixParents = new Dictionary<string, MimeType>(StringComparer.OrdinalIgnoreCase);
        foreach (var suffixParent in suffixParents)
        {
            normalizedSuffixParents.Add(
                suffixParent.Key,
                NormalizeCore(suffixParent.Value, groupsByMimeType)
            );
        }

        _suffixParents = normalizedSuffixParents.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        _parentsByMimeType = CreateParentLookup(parentRelations, groupsByMimeType);
        _groupsByExtension = CreateExtensionLookup(groups, extensionPreferences);
        _knownMimeTypes = CreateKnownMimeTypeLookup(
            groups,
            parentRelations,
            suffixParents,
            textParent,
            fallbackParent
        );
        _knownMimeTypesBySpan = _knownMimeTypes.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    public ImmutableArray<MimeTypeGroup> Groups { get; }

    public MimeTypeParseOptions ParseOptions { get; }

    public bool TryGetGroup(MimeType mimeType, [NotNullWhen(true)] out MimeTypeGroup? group)
    {
        if (mimeType.IsDefault)
        {
            group = null;
            return false;
        }

        return _groupsByMimeType.TryGetValue(mimeType, out group);
    }

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

    public MimeType Normalize(MimeType mimeType)
    {
        if (mimeType.IsDefault)
        {
            throw new ArgumentException("A default MIME type cannot be normalized.", nameof(mimeType));
        }

        return _groupsByMimeType.TryGetValue(mimeType, out var group) ? group.PrimaryMimeType : mimeType;
    }

    public MimeType Normalize(ReadOnlySpan<char> mimeType)
    {
        if (!TryResolveMimeType(mimeType, out var parsed))
        {
            throw new FormatException("The value is not a valid RFC 6838 media type name.");
        }

        return Normalize(parsed);
    }

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

    public bool TryNormalize(ReadOnlySpan<char> mimeType, out MimeType normalizedMimeType)
    {
        if (!TryResolveMimeType(mimeType, out var parsed))
        {
            normalizedMimeType = default;
            return false;
        }

        return TryNormalize(parsed, out normalizedMimeType);
    }

    public ImmutableArray<MimeTypeGroup> GetGroups(FileExtension extension)
    {
        return _groupsByExtension.GetValueOrDefault(extension, []);
    }

    public ImmutableArray<MimeTypeGroup> GetGroups(ReadOnlySpan<char> extension)
    {
        return GetGroups(FileExtension.Parse(extension));
    }

    public bool TryGetGroups(FileExtension extension, out ImmutableArray<MimeTypeGroup> groups)
    {
        return _groupsByExtension.TryGetValue(extension, out groups);
    }

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

    public bool TryGetPreferredGroup(
        ReadOnlySpan<char> extension,
        [NotNullWhen(true)] out MimeTypeGroup? group
    )
    {
        if (!FileExtension.TryParse(extension, out var parsed))
        {
            group = null;
            return false;
        }

        return TryGetPreferredGroup(parsed, out group);
    }

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

    public bool IsSubtypeOf(ReadOnlySpan<char> mimeType, ReadOnlySpan<char> potentialParent)
    {
        return TryResolveMimeType(mimeType, out var child) &&
               TryResolveMimeType(potentialParent, out var parent) &&
               IsSubtypeOf(child, parent);
    }

    public MimeTypeRegistryBuilder ToBuilder()
    {
        var builder = new MimeTypeRegistryBuilder(ParseOptions)
        {
            StructuredSyntaxSuffixRulesEnabled = _structuredSyntaxSuffixRulesEnabled,
            TextPlainRuleEnabled = _textPlainRuleEnabled,
            FallbackRuleEnabled = _fallbackRuleEnabled,
            TextParent = _sourceTextParent,
            FallbackParent = _sourceFallbackParent
        };

        builder.ClearSuffixParents();
        foreach (var suffixParent in _sourceSuffixParents)
        {
            builder.SetSuffixParent(suffixParent.Key, suffixParent.Value);
        }

        foreach (var group in Groups)
        {
            builder.AddGroup(group);
        }

        foreach (var relation in _sourceParentRelations)
        {
            builder.AddParent(relation.Child, relation.Parent);
        }

        foreach (var preference in _sourceExtensionPreferences)
        {
            builder.SetExtensionPreference(preference.Key, preference.Value);
        }

        return builder;
    }

    private bool TryResolveMimeType(ReadOnlySpan<char> value, out MimeType mimeType)
    {
        var mediaTypeLength = value.IndexOf(';');
        var lookupValue = mediaTypeLength >= 0 ? value[..mediaTypeLength] : value;
        while (!lookupValue.IsEmpty && lookupValue[^1] is ' ' or '\t')
        {
            lookupValue = lookupValue[..^1];
        }

        if (_knownMimeTypesBySpan.TryGetValue(lookupValue, out mimeType))
        {
            return true;
        }

        return MimeType.TryParse(value, ParseOptions, out mimeType);
    }

    private bool TryGetImplicitParent(MimeType mimeType, out MimeType parent)
    {
        if (_structuredSyntaxSuffixRulesEnabled &&
            mimeType.Suffix is not null &&
            _suffixParents.TryGetValue(mimeType.Suffix, out parent) &&
            parent != mimeType)
        {
            return true;
        }

        if (_textPlainRuleEnabled &&
            mimeType.TopLevelType == "text" &&
            mimeType != _normalizedTextPlain &&
            _textParent != mimeType)
        {
            parent = _textParent;
            return true;
        }

        if (_fallbackRuleEnabled && _fallbackParent != mimeType)
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
            return groups.ToImmutableArray();
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
        ImmutableArray<MimeTypeGroup> groups,
        ImmutableArray<(MimeType Child, MimeType Parent)> parentRelations,
        ImmutableArray<KeyValuePair<string, MimeType>> suffixParents,
        MimeType textParent,
        MimeType fallbackParent
    )
    {
        var knownMimeTypes = new Dictionary<string, MimeType>(StringComparer.OrdinalIgnoreCase);

        void Add(MimeType mimeType)
        {
            knownMimeTypes.TryAdd(mimeType.Value, mimeType);
        }

        foreach (var group in groups)
        {
            Add(group.PrimaryMimeType);
            foreach (var alias in group.Aliases)
            {
                Add(alias);
            }
        }

        foreach (var relation in parentRelations)
        {
            Add(relation.Child);
            Add(relation.Parent);
        }

        foreach (var suffixParent in suffixParents)
        {
            Add(suffixParent.Value);
        }

        Add(textParent);
        Add(fallbackParent);
        return knownMimeTypes.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }
}
