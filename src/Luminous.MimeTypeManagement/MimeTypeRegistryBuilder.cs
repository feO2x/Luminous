using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Luminous.MimeTypeManagement;

public sealed class MimeTypeRegistryBuilder
{
    private readonly Dictionary<FileExtension, List<MimeType>> _extensionPreferences = [];
    private readonly List<MimeTypeGroup> _groups = [];
    private readonly List<(MimeType Child, MimeType Parent)> _parentRelations = [];
    private readonly Dictionary<string, MimeType> _suffixParents = new (StringComparer.OrdinalIgnoreCase);
    private MimeTypeParseOptions _parseOptions;

    public MimeTypeRegistryBuilder(MimeTypeParseOptions? parseOptions = null)
    {
        _parseOptions = parseOptions ?? MimeTypeParseOptions.Default;
        _suffixParents.Add("xml", MimeType.Parse("application/xml"));
        _suffixParents.Add("json", MimeType.Parse("application/json"));
        _suffixParents.Add("zip", MimeType.Parse("application/zip"));
        TextParent = MimeType.Parse("text/plain");
        FallbackParent = MimeType.Parse("application/octet-stream");
    }

    public MimeTypeParseOptions ParseOptions
    {
        get => _parseOptions;
        set => _parseOptions = value ?? throw new ArgumentNullException(nameof(value));
    }

    public bool StructuredSyntaxSuffixRulesEnabled { get; set; } = true;

    public bool TextPlainRuleEnabled { get; set; } = true;

    public bool FallbackRuleEnabled { get; set; } = true;

    public MimeType TextParent { get; set; }

    public MimeType FallbackParent { get; set; }

    public IReadOnlyList<MimeTypeGroup> Groups => _groups;

    public MimeTypeRegistryBuilder AddGroup(MimeTypeGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        _groups.Add(group);
        return this;
    }

    public MimeTypeRegistryBuilder AddGroup(
        MimeType primaryMimeType,
        IEnumerable<MimeType>? aliases = null,
        IEnumerable<FileExtension>? extensions = null
    ) =>
        AddGroup(new MimeTypeGroup(primaryMimeType, aliases, extensions));

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

    public bool RemoveGroup(MimeType primaryMimeType) =>
        _groups.RemoveAll(group => group.PrimaryMimeType == primaryMimeType) > 0;

    public bool RemoveGroup(ReadOnlySpan<char> primaryMimeType) =>
        MimeType.TryParse(primaryMimeType, ParseOptions, out var parsed) && RemoveGroup(parsed);

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

    public MimeTypeRegistryBuilder AddParent(MimeType child, MimeType parent)
    {
        if (!_parentRelations.Contains((child, parent)))
        {
            _parentRelations.Add((child, parent));
        }

        return this;
    }

    public MimeTypeRegistryBuilder AddParent(ReadOnlySpan<char> child, ReadOnlySpan<char> parent) =>
        AddParent(MimeType.Parse(child, ParseOptions), MimeType.Parse(parent, ParseOptions));

    public MimeTypeRegistryBuilder AddParentRelation(MimeType child, MimeType parent) => AddParent(child, parent);

    public bool RemoveParent(MimeType child, MimeType parent) => _parentRelations.Remove((child, parent));

    public bool RemoveParentRelation(MimeType child, MimeType parent) => RemoveParent(child, parent);

    public int ClearParents(MimeType child) => _parentRelations.RemoveAll(relation => relation.Child == child);

    public MimeTypeRegistryBuilder SetSuffixParent(string suffix, MimeType parent)
    {
        _suffixParents[NormalizeSuffix(suffix)] = parent;
        return this;
    }

    public MimeTypeRegistryBuilder SetSuffixParent(string suffix, ReadOnlySpan<char> parent) =>
        SetSuffixParent(suffix, MimeType.Parse(parent, ParseOptions));

    public bool RemoveSuffixParent(string suffix) => _suffixParents.Remove(NormalizeSuffix(suffix));

    public void ClearSuffixParents() => _suffixParents.Clear();

    public MimeTypeRegistryBuilder SetExtensionPreference(
        FileExtension extension,
        IEnumerable<MimeType> orderedGroupPrimaries
    )
    {
        ArgumentNullException.ThrowIfNull(orderedGroupPrimaries);
        _extensionPreferences[extension] = orderedGroupPrimaries.ToList();
        return this;
    }

    public MimeTypeRegistryBuilder SetExtensionPreference(
        FileExtension extension,
        params MimeType[] orderedGroupPrimaries
    ) =>
        SetExtensionPreference(extension, (IEnumerable<MimeType>) orderedGroupPrimaries);

    public bool ClearExtensionPreference(FileExtension extension) => _extensionPreferences.Remove(extension);

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
