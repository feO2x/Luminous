# Luminous

Luminous provides open-source components for document management systems.

## Luminous.MimeTypeManagement

[![NuGet](https://img.shields.io/nuget/v/Luminous.MimeTypeManagement.svg)](https://www.nuget.org/packages/Luminous.MimeTypeManagement)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A dependency-free .NET library that answers the three MIME type questions every document
management system has to answer:

1. **"What should I store?"** — Browsers, operating systems, and e-mail clients report different
   names for the same format (`image/jpeg` vs. `image/jpg`, `application/zip` vs.
   `application/x-zip-compressed`). Normalization collapses all of them into one canonical value
   that belongs in your database.
2. **"What kind of file is this?"** — Map file extensions to MIME types, even when an extension
   is ambiguous (`.webm` can be audio or video).
3. **"Can my handler process this?"** — Ask whether one format derives from another. A DOCX file
   *is* a ZIP container, and an EPUB is one too — without either of them ever normalizing to
   `application/zip`.

### Installation

```shell
dotnet add package Luminous.MimeTypeManagement
```

Requires .NET 10. The library has no third-party dependencies.

### Quick start

For document formats, start with the built-in, hand-curated registry — it covers about 90 formats
from office documents and images to e-mail and archives:

```csharp
using Luminous.MimeTypeManagement;

var registry = DocumentSeed.Registry;

// 1. Normalize a supported MIME type into one canonical value.
//    Casing and Content-Type parameters never get in the way.
registry.Normalize("image/jpg");                    // image/jpeg
registry.Normalize("application/x-pdf");            // application/pdf
registry.Normalize("TEXT/HTML; charset=utf-8");     // text/html

// 2. Map a file extension to a MIME type.
if (registry.TryGetPreferredGroup(".jpg", out var group))
    Console.WriteLine(group.PrimaryMimeType);       // image/jpeg

// 3. Ask compatibility questions — without changing identity.
registry.IsSubtypeOf(
    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    "application/zip");                             // true: DOCX is a ZIP container
```

`DocumentSeed.Registry` is a shared, lazily created singleton. `MimeTypeRegistry` is immutable and
thread-safe, so you can also register a registry built from your own configuration as a singleton
in your DI container.

### Core concepts

#### Equivalence groups and normalization

A **group** describes *one* format: the canonical **primary MIME type** your application should
store, the **aliases** the format is known under in the wild, and its **file extensions**.
Normalizing any group member — primary or alias — always yields the primary:

```csharp
registry.Normalize("audio/wav");     // audio/vnd.wave (the IANA-registered name)
registry.Normalize("audio/x-wav");   // audio/vnd.wave
```

Valid MIME types that are unknown to the registry throw `KeyNotFoundException` by default. This
makes normalization suitable for rejecting unsupported document types. Pass
`throwWhenUnknown: false` to preserve an unknown vendor-specific value instead:

```csharp
registry.Normalize("application/vnd.acme.invoice+json", throwWhenUnknown: false);
```

#### Hierarchy and `IsSubtypeOf`

Aliases are for names that mean *the same* format. For "is-a" and "is-based-on" relationships,
the registry maintains a **hierarchy** instead. DOCX is based on ZIP, but it must never normalize
to `application/zip` — so the seed models it with a parent relation, not an alias:

```csharp
var docx = MimeType.Parse(
    "application/vnd.openxmlformats-officedocument.wordprocessingml.document");

registry.IsSubtypeOf(docx, "application/zip");  // true
registry.Normalize(docx);                       // still application/vnd...document
```

A type's parents come from three implicit rules — all enabled by default — unless an explicit
parent relation overrides them for that type:

| Rule | Default behavior |
|---|---|
| Structured-syntax suffix | `+xml` → `application/xml`, `+json` → `application/json`, `+zip` → `application/zip` |
| `text/*` rule | any `text/*` type derives from `text/plain` |
| Fallback rule | everything else derives from `application/octet-stream` |

```csharp
registry.IsSubtypeOf("application/epub+zip", "application/zip");   // suffix rule
registry.IsSubtypeOf("text/csv", "text/plain");                    // text rule
registry.IsSubtypeOf("image/jpeg", "application/octet-stream");    // fallback rule
```

`IsSubtypeOf` is transitive and reflexive, which makes it suitable for capability checks such as
"can this ZIP extractor accept this upload?".

#### File extensions and ambiguity

An extension alone cannot reliably identify file content. When several groups claim the same
extension, `GetGroups` returns all candidates in configured preference order, while
`TryGetPreferredGroup` gives you only the first:

```csharp
// The document seed registers .webm for both audio/webm and video/webm,
// with video preferred.
var groups = registry.GetGroups(".webm");
var firstCandidate = groups[0].PrimaryMimeType;  // video/webm
var secondCandidate = groups[1].PrimaryMimeType; // audio/webm

if (registry.TryGetPreferredGroup(".webm", out var preferred))
    Console.WriteLine(preferred.PrimaryMimeType); // video/webm
```

Extension parsing is forgiving — `webm`, `.webm`, and `*.WEBM` all work, and compound extensions
like `.tar.gz` are preserved.

### Customizing the document seed

The seed's primary MIME types follow a canonical-selection policy: current RFC and IANA
registrations win over legacy and de-facto names, which are kept as aliases. If your application
prefers different primaries, needs additional formats, or wants only a subset of the categories,
customize a builder and build your own immutable registry:

```csharp
// Start from the full seed and extend it...
var builder = DocumentSeed.CreateBuilder();
builder.AddGroup("application/vnd.acme.invoice+json", extensions: [".acmeinv"]);
builder.ReplaceGroup(
    MimeType.Parse("audio/vnd.wave"),
    new MimeTypeGroup(
        MimeType.Parse("audio/wav"),
        aliases: [MimeType.Parse("audio/vnd.wave"), MimeType.Parse("audio/x-wav")],
        extensions: [FileExtension.Parse(".wav")]
    )
);

// ...or reshape the already built shared registry without affecting its readers.
var customized = DocumentSeed.Registry.ToBuilder()
    .AddGroup("application/vnd.acme.archive", extensions: [".acme"])
    .Build();
```

To compose a leaner seed from scratch, call only the category methods you need —
`DocumentSeed.AddOfficeFormats`, `AddImageFormats`, `AddAudioVideoFormats`, `AddWebAndDataFormats`,
`AddEmailAndArchiveFormats`, and `AddAcademicFormats` — on a fresh `MimeTypeRegistryBuilder`.

### Building a registry from scratch

```csharp
var registry = new MimeTypeRegistryBuilder()
    .AddGroup("application/zip", ["application/x-zip-compressed"], ["zip"])
    .AddGroup(
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        extensions: ["docx"]
     )
    .AddParent(
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/zip"
     )
    .Build();

registry.Normalize("application/x-zip-compressed"); // application/zip
```

Validation is deferred to `Build()`, which reports **all** detected problems — duplicate group
membership, inconsistent extension preferences, hierarchy cycles, and more — in a single
`MimeTypeRegistryValidationException` with a `Violations` collection you can inspect
programmatically. This makes it practical to assemble a registry from several data sources before
resolving conflicts. Registries can also be created directly from a `MimeTypeRegistryConfiguration`,
for example after deserializing configuration data.

### API overview

| Type | Purpose |
|---|---|
| `MimeType` | An RFC 6838 media type name, normalized (lowercase, no parameters) for reliable comparison. Rejects wildcards like `image/*`. |
| `FileExtension` | A case-normalized, dot-prefixed file extension. |
| `MimeTypeGroup` | One format: primary MIME type, aliases, and ordered file extensions. |
| `MimeTypeRegistry` | Immutable, thread-safe normalization, extension lookup, and hierarchy queries. |
| `MimeTypeRegistryBuilder` | Mutable, fluent configuration; `Build()` validates and creates an immutable snapshot. |
| `MimeTypeRegistryConfiguration` | The complete registry as plain data, for direct construction or deserialization. |
| `MimeTypeParseOptions` | Input-length guard for parsing (RFC 6838 default: 127 characters per component). |
| `MimeTypeRegistryValidationException` | Reports every configuration violation detected in one pass. |
| `DocumentSeed` | Hand-curated registry of ~90 document formats: shared `Registry`, `CreateBuilder()`, and per-category composition methods. |

## License

This project is licensed under the MIT License. The Luminous.InSign test project depends on
PdfPig, which is licensed under the Apache License 2.0. See THIRD-PARTY-NOTICES.md for details.
