# Core MIME Type Management Model

## Rationale

Document management systems receive MIME types from unreliable sources: browsers and operating systems report the same format under different names (`application/x-zip-compressed` vs. `application/zip`, `text/xml` vs. `application/xml`, `image/pjpeg` vs. `image/jpeg`). Storing these values unnormalized fragments search, preview, and retention logic. No existing .NET library models MIME type equivalence with a canonical primary, and Apache Tika's registry — the closest conceptual match — is add-only, not removable/overridable, and Java-only.

This plan establishes the in-memory core of `Luminous.MimeTypeManagement`: a parsed `MimeType` value object, equivalence groups with a primary MIME type, bidirectional file-extension mappings, a separate configurable "is-a" hierarchy, and an immutable registry built via a validating builder. Seeding from external data sources (freedesktop XML, mime-db, a curated default registry) and content-based detection are deliberately out of scope and will follow in later plans.

## Acceptance Criteria

- [x] A new project `src/Luminous.MimeTypeManagement/Luminous.MimeTypeManagement.csproj` exists, has no external package dependencies, and builds without warnings in Release. Both it (under a new `/src/` folder) and the test project (under the existing `/tests/` folder) are referenced in `Luminous.slnx`.
- [x] `MimeType` parses RFC 6838 media type names case-insensitively into a normalized lowercase form, exposes top-level type, subtype, and the RFC 6839 structured-syntax suffix, tolerates and discards parameters (e.g. `; charset=utf-8`), and rejects input violating RFC 6838's restricted-name grammar via both `TryParse` and a throwing `Parse`. Type and subtype names exceeding a configurable length limit (default: 127 characters each, per RFC 6838 §4.2) are rejected before any allocation occurs.
- [x] `FileExtension` normalizes the input forms `pdf`, `.pdf`, `*.pdf` (any casing) to a canonical lowercase dotted form, supports compound extensions such as `.tar.gz`, and rejects invalid input.
- [x] The registry resolves any group member — primary or alias — to its `MimeTypeGroup`, and normalization returns the group's primary MIME type. Unknown MIME types pass through `Normalize` unchanged and yield `false` from `TryNormalize`.
- [x] Extension lookups work in both directions: a group exposes its ordered extensions with a primary extension, and an extension resolves to all groups claiming it, ordered by preference, with the preferred group available through a single-result lookup.
- [x] The hierarchy answers `IsSubtypeOf` reflexively (every type is a subtype of itself) and transitively across explicit parent relations and configurable implicit rules (structured-syntax suffix mappings, `text/*` → `text/plain`, fallback to `application/octet-stream`); explicit relations take precedence over implicit rules, and every implicit rule can be reconfigured or disabled.
- [x] `Build()` rejects invalid configurations — a MIME type belonging to more than one group, duplicates within a group, cycles in the explicit hierarchy — with a single exception reporting all violations.
- [x] `MimeTypeRegistry` is immutable and safe for concurrent reads; `ToBuilder()` produces a builder that can modify (including remove and override) every aspect of an existing registry and build a new one.
- [x] Unit tests in `tests/Luminous.MimeTypeManagement.Tests` cover all criteria above with code coverage above 95%.

## Technical Details

**Project setup.** Target inherits from `Directory.Build.props` (net10.0, explicit usings, nullable enabled). The test project mirrors `Luminous.InSign.Tests` (xunit.v3, FluentAssertions, Microsoft.Testing.Platform). Add a `/src/` folder to `Luminous.slnx`. All types are `public` per the root AGENTS.md.

**`MimeType`** is a readonly struct storing the normalized lowercase `type/subtype` string. Parameters are not part of a MIME type's identity in this library; parsing accepts them and discards them (parameterized registry entries à la Tika's `;version=2` are explicitly out of scope). Equality is value-based on the normalized string. The component properties are plain eagerly-created `string`s — do not substitute `ReadOnlyMemory<char>` or lazy slicing here; parsing is not a hot path, and allocation-free lookup for known types is the registry's job (see below). Validity follows RFC 6838 §4.2's restricted-name grammar: type and subtype names must start with an alphanumeric character, followed by alphanumerics or `!#$&-^_.+`, with `+` treated as the structured-syntax suffix delimiter (the suffix is the segment after the last `+`). Parsing also enforces the same section's 127-character limit on the type and subtype names, rejecting over-long input before allocating — this is the DoS guard for hostile `Content-Type` values. The limit is configurable via an options overload, e.g. `TryParse(value, MimeTypeParseOptions, out MimeType)`. `MimeTypeParseOptions` is a sealed immutable class (not a struct — `default(T)` of a struct would carry a degenerate zero limit) with init-only properties, carrying `MaxNameLength` (applied per component; default 127) and exposing a static `Default` instance with the RFC values; overloads without an options parameter forward to `Default`, so the default behavior is defined in exactly one place. Illustrative shape:

```csharp
public readonly struct MimeType : IEquatable<MimeType>
{
    public static bool TryParse(ReadOnlySpan<char> value, out MimeType mimeType);
    public static MimeType Parse(ReadOnlySpan<char> value);
    public string Value { get; }        // "application/vnd.ms-excel"
    public string TopLevelType { get; } // "application"
    public string SubType { get; }      // "vnd.ms-excel"
    public string? Suffix { get; }      // "xml" for "+xml", null otherwise
}
```

**`FileExtension`** is a readonly struct storing the canonical lowercase dotted form (`.tar.gz`). Input containing path separators, whitespace, or wildcard characters other than a leading `*.` is invalid.

**`MimeTypeGroup`** is a sealed immutable class: `PrimaryMimeType`, `Aliases` (possibly empty), `Extensions` ordered by preference (possibly empty; first entry is `PrimaryExtension`, which is null-like/absent for extension-less types). Groups model *same format, different names* only — container relationships (docx is physically a zip) belong to the hierarchy, never to aliasing.

**Hierarchy** is kept structurally separate from groups. Explicit parent relations are edges between canonical MIME types; multiple parents per type are allowed, and a parent does not need to belong to any group (e.g. `application/octet-stream`). Implicit rules are configuration data on the builder, not hardcoded behavior, applied only when no explicit relation exists for the queried type, in this order:

1. structured-syntax suffix mappings — defaults: `+xml` → `application/xml`, `+json` → `application/json`, `+zip` → `application/zip`; user-extensible per suffix;
2. `text/*` (except `text/plain`) → `text/plain`;
3. ultimate fallback → `application/octet-stream` (making everything a subtype of it, as in Tika).

Each rule can be disabled independently. `IsSubtypeOf` normalizes both arguments before walking the graph and uses subtype-or-equal semantics: equal (normalized) types answer `true`. The dominant use case is capability/accept checks ("can the zip handler open this?"), which must include the type itself; a strict-specialization variant is deliberately omitted until a concrete need arises.

**`MimeTypeRegistry`** is sealed, immutable, and backed by `FrozenDictionary` lookups keyed by `MimeType` (any member → group) and `FileExtension` (→ ordered groups). String-accepting overloads resolve known types without parsing: a string-keyed lookup table using `StringComparer.OrdinalIgnoreCase` and its span-based alternate lookup (`GetAlternateLookup<ReadOnlySpan<char>>`) returns the registry's pre-built `MimeType` instances allocation-free; input carrying parameters is sliced at the `';'` before the lookup. Full `MimeType` parsing runs only as the fallback for unknown input, and unparseable input yields a negative answer rather than throwing; the builder accepts an optional `MimeTypeParseOptions` that this fallback uses, so registry users get the same length-limit escape hatch. No MIME types derived from input strings are cached beyond the registry's own members — lookup input is untrusted, and an unbounded intern cache would be a memory-DoS vector. `Normalize` deliberately passes unknown types through unchanged (lowercased/parsed) — a DMS wants graceful passthrough for exotic but valid types.

**`MimeTypeRegistryBuilder`** is mutable and not thread-safe. It supports adding and removing groups and parent relations, reordering extension preference (default preference order is registration order; overridable via an explicit re-prioritization call), and configuring the implicit hierarchy rules. Unlike Tika's add-only registry, removal and override are first-class: `ToBuilder()` on a registry must round-trip completely so a future shipped default registry can be customized by users. `Build()` collects *all* violations and throws one dedicated exception (e.g. `MimeTypeRegistryValidationException`) listing them; validation covers unique group membership across all primaries and aliases, intra-group duplicates, and cycles among explicit parent relations. Extensions claimed by multiple groups are valid by design (`.stl` is both a subtitle and a 3D model format); the ambiguity is exposed via the ordered multi-result lookup.

**Testing.** Prefer sociable tests through `MimeTypeRegistryBuilder`/`MimeTypeRegistry` — build small registries in the tests and assert observable lookup behavior; solitary tests only for `MimeType`/`FileExtension` parsing edge cases and guard clauses. The known real-world normalization cases (`application/x-zip-compressed` → `application/zip`, `text/xml` → `application/xml`, `image/pjpeg` → `image/jpeg`) and a container-vs-alias case (docx is-a `application/zip` via hierarchy but never normalizes to it) must appear as test scenarios. Include a concurrency smoke test performing parallel reads on a shared registry, and an allocation-assertion test that measures the warm known-type string-lookup path with `GC.GetAllocatedBytesForCurrentThread` and asserts zero allocations — this guards the registry's allocation-free lookup requirement without a benchmark project.
