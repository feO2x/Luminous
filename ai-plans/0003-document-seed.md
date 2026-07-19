# Initial Document Seed for the MIME Type Registry

## Rationale

`Luminous.MimeTypeManagement` currently ships only empty builders: every consumer must assemble groups, aliases, hierarchy edges, and extension mappings from scratch. This plan adds a first-party, hand-curated seed covering formats relevant to document management systems, so consumers get useful normalization and extension lookups out of the box while retaining full configurability through the existing builder APIs.

The coverage target is the format list of [Xberg](https://github.com/xberg-io/xberg) as of July 2026 (~97 formats across office, image, audio/video, web/data, email/archive, and academic categories); later Xberg additions do not retroactively change this plan's scope. This scope is large enough to exercise every core feature (equivalence groups, hierarchy, multi-group extensions, compound extensions) yet small enough to curate and review by hand — deliberately deferring import pipelines for large external registries (tika-mimetypes.xml, mime-db) to later plans.

## Acceptance Criteria

- [ ] A public static class `DocumentSeed` in `Luminous.MimeTypeManagement` exposes a shared immutable `Registry` property and a `CreateBuilder()` method returning a fresh, fully populated `MimeTypeRegistryBuilder` that callers can modify before calling `Build()`.
- [ ] Every file extension in the pinned Xberg snapshot (July 2026, stored as static test data, no network I/O) resolves to at least one group in the seeded registry.
- [ ] The litmus normalizations work out of the box: `application/x-zip-compressed` → `application/zip`, `text/xml` → `application/xml`, `image/pjpeg` → `image/jpeg`.
- [ ] ZIP-based container formats without a `+zip` suffix (OOXML, ODF, iWork) are subtypes of `application/zip` via explicit hierarchy edges, while suffix-carrying types reach their parents through the structured-syntax suffix rules alone (`application/epub+zip` → `application/zip`, `image/svg+xml` → `application/xml`).
- [ ] `.webm` resolves to both the `audio/webm` and `video/webm` groups, with `video/webm` as the preferred group.
- [ ] Every group's primary MIME type follows the canonical-selection policy documented in Technical Details.
- [ ] The seeded registry contains exactly the groups, aliases, extensions, parent edges, and preference orders specified in the List of MIME Type Groups section of this plan.
- [ ] The seeded builder builds without validation errors, the test suite covers the seed through sociable tests against the public API, overall coverage stays above 95%, the Release build is warning-free, and no new package dependencies are introduced.

## Technical Details

**API shape** (exact):

```csharp
public static class DocumentSeed
{
    public static MimeTypeRegistry Registry { get; } // shared, lazily created
    public static MimeTypeRegistryBuilder CreateBuilder();
}
```

`CreateBuilder()` returns a new builder per call because builders are mutable and not thread-safe; `Registry` may be a shared lazy singleton because `MimeTypeRegistry` is immutable. The name is deliberately neither "Default" (which would over-promise universal coverage) nor "Xberg" (which would couple the public API to a third-party project). Future seeds with different scopes can sit alongside it under the same naming pattern.

**Data representation**: hand-written C# calls against `MimeTypeRegistryBuilder`, organized by category (office, images, audio/video, web/data, email/archives, academic) in private methods or partial class files. No embedded XML/JSON resources and no parsing at seed time — compile-time checking and reviewable diffs are the point of this first seed.

**Canonical-selection policy** (applied in order; de-facto and legacy variants observed in the wild become aliases):

1. When several types are registered or common for a format and a current RFC or the IANA registry designates one as preferred, use it (e.g., `application/xml` over `text/xml`).
2. Otherwise the IANA-registered type wins, even where legacy names remain widespread (e.g., `application/vnd.rar` primary, `application/x-rar-compressed` alias; `application/gzip` primary, `application/x-gzip` alias; `text/markdown` primary, `text/x-markdown` alias).
3. For unregistered formats, the most widespread de-facto type wins, typically `x-`-prefixed (e.g., `application/x-tar`, `application/x-7z-compressed`).

This policy is mechanically checkable against the IANA registry, stable over time, and normalizes in the legacy-to-standard direction. Consumers who prefer a de-facto type as primary swap it via `ToBuilder()`/`ReplaceGroup`.

**Curation sources**: Xberg supplies only the extension list. MIME types, aliases, hierarchy edges, and extension preference orders are curated independently from IANA registrations and well-known de-facto usage; no Xberg code or data files are copied.

**Hierarchy**: ZIP-based containers get explicit `AddParent` edges to `application/zip`. Legacy OLE-based formats (`.doc`, `.xls`, `.ppt`, `.msg`) get `application/x-ole-storage` as parent — a bare parent without its own group, which the core model supports. `+xml`/`+json`/`+zip` types rely on the built-in suffix rules; do not duplicate them as explicit edges.

**Group table**: the authoritative curation lives in the List of MIME Type Groups section below, which lists every group (primary, aliases, extensions, explicit parents) plus bare parents and extension preference orders, and documents the methodology used to derive each row. The implementer transcribes that table into builder calls without re-deciding mappings; disagreements with the table are resolved by amending the table first.

**Testing**: pin the Xberg extension list (July 2026 snapshot) as xunit Theory Data in the test project and assert in one sociable Theory test method that every entry resolves through `DocumentSeed.Registry`. A mirror test transcribes the group table a second time into static test data — primary, aliases, extensions, and explicit parents per group — and asserts that the built registry matches it exactly, including the total group count of 89. This double-entry transcription is what makes the "exactly" acceptance criterion verifiable: a transcription error must be made identically twice to go undetected, and it also covers the hierarchy edges (including the `application/x-ole-storage` parents) that the scenario tests do not reach. Additional sociable tests cover the litmus normalizations, the docx container-versus-alias distinction, the `.webm` preference order, and that `CreateBuilder().Build()` round-trips to an equivalent registry.

### List of MIME Type Groups

Each row was derived as follows:

1. Look the extension's format up in the [IANA media-types registry](https://www.iana.org/assignments/media-types/media-types.xhtml).
2. Cross-check the freedesktop `shared-mime-info` database and Apache Tika's `tika-mimetypes.xml` as *evidence of de-facto usage and aliases* (no data copied verbatim; every row re-judged under our policy).
3. Apply the canonical-selection policy above: RFC/IANA-preferred type → IANA-registered type → most widespread de-facto type (`x-`-prefixed by convention). Other names observed in the wild become aliases.

Parents reachable through the built-in structured-syntax suffix rules (`+xml`, `+json`, `+zip`) are **not** listed — only explicit `AddParent` edges are.

#### Bare Parents (no group of their own)

| Type | Used as parent of |
| --- | --- |
| `application/x-ole-storage` | Legacy OLE/CFB formats: `application/msword`, `application/vnd.ms-excel`, `application/vnd.ms-powerpoint`, `application/vnd.ms-outlook`, `application/x-hwp` |

#### Office — Word Processing

| Primary | Aliases | Extensions | Parent | Notes |
| --- | --- | --- | --- | --- |
| `application/vnd.openxmlformats-officedocument.wordprocessingml.document` | — | `.docx` | `application/zip` | |
| `application/vnd.ms-word.document.macroenabled.12` | — | `.docm` | `application/zip` | IANA-registered (case-insensitive match of `macroEnabled`) |
| `application/vnd.openxmlformats-officedocument.wordprocessingml.template` | — | `.dotx` | `application/zip` | |
| `application/vnd.ms-word.template.macroenabled.12` | — | `.dotm` | `application/zip` | |
| `application/msword` | `application/x-msword` | `.doc`, `.dot` | `application/x-ole-storage` | `.dot` = Word template, same format |
| `application/vnd.oasis.opendocument.text` | — | `.odt` | `application/zip` | |
| `application/vnd.apple.pages` | `application/x-iwork-pages-sffpages` | `.pages` | `application/zip` | |

#### Office — Spreadsheets

| Primary | Aliases | Extensions | Parent | Notes |
| --- | --- | --- | --- | --- |
| `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` | — | `.xlsx` | `application/zip` | |
| `application/vnd.ms-excel.sheet.macroenabled.12` | — | `.xlsm` | `application/zip` | |
| `application/vnd.ms-excel.sheet.binary.macroenabled.12` | — | `.xlsb` | `application/zip` | |
| `application/vnd.ms-excel.template.macroenabled.12` | — | `.xltm` | `application/zip` | |
| `application/vnd.openxmlformats-officedocument.spreadsheetml.template` | — | `.xltx` | `application/zip` | |
| `application/vnd.ms-excel.addin.macroenabled.12` | — | `.xlam` | `application/zip` | |
| `application/vnd.ms-excel` | `application/x-msexcel`, `application/excel` | `.xls`, `.xlt`, `.xla` | `application/x-ole-storage` | `.xlt` template, `.xla` add-in — same OLE format |
| `application/vnd.oasis.opendocument.spreadsheet` | — | `.ods` | `application/zip` | |
| `application/vnd.apple.numbers` | `application/x-iwork-numbers-sffnumbers` | `.numbers` | `application/zip` | |

#### Office — Presentations

| Primary | Aliases | Extensions | Parent | Notes |
| --- | --- | --- | --- | --- |
| `application/vnd.openxmlformats-officedocument.presentationml.presentation` | — | `.pptx` | `application/zip` | |
| `application/vnd.ms-powerpoint.presentation.macroenabled.12` | — | `.pptm` | `application/zip` | |
| `application/vnd.openxmlformats-officedocument.presentationml.slideshow` | — | `.ppsx` | `application/zip` | |
| `application/vnd.openxmlformats-officedocument.presentationml.template` | — | `.potx` | `application/zip` | |
| `application/vnd.ms-powerpoint.template.macroenabled.12` | — | `.potm` | `application/zip` | |
| `application/vnd.ms-powerpoint` | `application/x-mspowerpoint`, `application/powerpoint` | `.ppt`, `.pot` | `application/x-ole-storage` | |
| `application/vnd.oasis.opendocument.presentation` | — | `.odp` | `application/zip` | |
| `application/vnd.apple.keynote` | `application/x-iwork-keynote-sffkey` | `.key` | `application/zip` | |

#### PDF, eBooks, Database, Hangul

| Primary | Aliases | Extensions | Parent | Notes |
| --- | --- | --- | --- | --- |
| `application/pdf` | `application/x-pdf` | `.pdf` | — | |
| `application/epub+zip` | — | `.epub` | — | zip parent via `+zip` suffix rule |
| `application/x-fictionbook+xml` | — | `.fb2` | — | unregistered; shared-mime-info name; xml parent via suffix rule |
| `application/x-dbf` | `application/dbf`, `application/dbase` | `.dbf` | — | unregistered; `x-dbf` per shared-mime-info |
| `application/x-hwp` | `application/haansofthwp`, `application/vnd.hancom.hwp` | `.hwp` | `application/x-ole-storage` | unregistered |
| `application/hwp+zip` | `application/vnd.hancom.hwpx` | `.hwpx` | — | zip parent via suffix rule |

#### Images

| Primary | Aliases | Extensions       | Parent | Notes |
| --- | --- |------------------| --- | --- |
| `image/png` | `image/x-png` | `.png`           | — | |
| `image/jpeg` | `image/pjpeg`, `image/jpg` | `.jpg`, `.jpeg`  | — | litmus case; `image/jpg` is a widespread mistake worth normalizing |
| `image/gif` | — | `.gif`           | — | |
| `image/webp` | — | `.webp`          | — | |
| `image/bmp` | `image/x-ms-bmp`, `image/x-bmp` | `.bmp`           | — | |
| `image/tiff` | — | `.tiff`, `.tif`  | — | |
| `image/jp2` | — | `.jp2`           | — | RFC 3745 |
| `image/jpx` | — | `.jpx`           | — | RFC 3745 |
| `image/jpm` | — | `.jpm`           | — | RFC 3745 |
| `video/mj2` | — | `.mj2`           | — | RFC 3745; Motion JPEG 2000 is registered under `video/` |
| `image/x-jbig2` | — | `.jbig2`, `.jb2` | — | unregistered |
| `image/x-portable-anymap` | — | `.pnm`           | — | |
| `image/x-portable-bitmap` | — | `.pbm`           | `image/x-portable-anymap` | |
| `image/x-portable-graymap` | — | `.pgm`           | `image/x-portable-anymap` | |
| `image/x-portable-pixmap` | — | `.ppm`           | `image/x-portable-anymap` | |
| `image/heif` | — | `.heif`          | — | |
| `image/heic` | — | `.heic`          | `image/heif` | HEIC is HEIF with HEVC coding |
| `image/heic-sequence` | — | `.heics`         | — | optional parent `image/heif-sequence` omitted (no extension in scope) |
| `image/avif` | — | `.avif`, `.avcs`        | — | |
| `image/svg+xml` | — | `.svg`           | — | xml parent via suffix rule; covers Xberg's vector *and* markup listing |

#### Audio & Video

| Primary | Aliases | Extensions | Parent | Notes                                                                                                                |
| --- | --- | --- | --- |----------------------------------------------------------------------------------------------------------------------|
| `audio/mpeg` | `audio/mp3`, `audio/x-mp3` | `.mp3`, `.mpga` | — |                                                                                                                      |
| `audio/mp4` | `audio/x-m4a` | `.m4a` | — |                                                                                                                      |
| `audio/vnd.wave` | `audio/wav`, `audio/x-wav`, `audio/wave` | `.wav` | — | **policy stress case**: IANA-registered primary vs. overwhelmingly dominant `audio/wav` - we stick with IANA default |
| `audio/webm` | — | `.webm` | — | shared extension, see preference order                                                                               |
| `video/mp4` | — | `.mp4` | — |                                                                                                                      |
| `video/mpeg` | — | `.mpeg` | — |                                                                                                 |
| `video/webm` | — | `.webm` | — |                                                                                                                      |

#### Web & Data

| Primary | Aliases | Extensions | Parent | Notes |
| --- | --- | --- | --- | --- |
| `text/html` | — | `.html`, `.htm` | — | |
| `application/xhtml+xml` | — | `.xhtml` | — | xml parent via suffix rule |
| `application/xml` | `text/xml` | `.xml` | — | litmus case |
| `application/json` | `text/json`, `text/x-json` | `.json` | — | |
| `application/yaml` | `application/x-yaml`, `text/yaml`, `text/x-yaml` | `.yaml`, `.yml` | — | RFC 9512 |
| `application/toml` | `application/x-toml` | `.toml` | — | |
| `text/csv` | `application/csv`, `text/x-csv` | `.csv` | — | RFC 4180 |
| `text/tab-separated-values` | `text/tsv` | `.tsv` | — | |

#### Text & Markup

| Primary | Aliases | Extensions | Parent | Notes                                                     |
| --- | --- | --- | --- |-----------------------------------------------------------|
| `text/plain` | — | `.txt` | — | implicit parent of all `text/*` via built-in rule         |
| `text/markdown` | `text/x-markdown` | `.md`, `.markdown` | — | RFC 7763                                                  |
| `text/x-djot` | — | `.djot` | — | no established type; using naming convention              |
| `text/mdx` | — | `.mdx` | — | used by the mdx-js project; no registration               |
| `text/x-rst` | — | `.rst` | — | docutils convention                                       |
| `text/org` | `text/x-org` | `.org` | — | both circulate; `text/org` chosen as primary              |
| `application/rtf` | `text/rtf` | `.rtf` | — | both IANA-registered; `application/rtf` chosen as primary |

#### Email & Archives

| Primary | Aliases | Extensions | Parent | Notes |
| --- | --- | --- | --- | --- |
| `message/rfc822` | — | `.eml` | — | |
| `application/vnd.ms-outlook` | — | `.msg` | `application/x-ole-storage` | |
| `application/vnd.ms-outlook-pst` | — | `.pst` | — | unregistered; Tika's name. No OLE parent — PST is its own container format, not CFB |
| `application/zip` | `application/x-zip-compressed`, `application/x-zip` | `.zip` | — | litmus case; also parent of container formats |
| `application/x-tar` | — | `.tar` | — | unregistered; universal de-facto name |
| `application/gzip` | `application/x-gzip` | `.gz`, `.tgz`, `.tar.gz` | — | RFC 6713; `.tar.gz` exercises compound extensions |
| `application/x-7z-compressed` | — | `.7z` | — | unregistered; universal de-facto name |

#### Academic & Scientific

| Primary | Aliases | Extensions | Parent | Notes                                                |
| --- | --- | --- | --- |------------------------------------------------------|
| `text/x-bibtex` | `application/x-bibtex` | `.bib` | — |                                                      |
| `application/x-research-info-systems` | — | `.ris` | — | Zotero/citation-manager convention                   |
| `application/x-nbib` | — | `.nbib` | — | no established type; using naming convention         |
| `application/x-endnote-refer` | — | `.enw` | — | Tika/shared-mime-info-derived                        |
| `text/x-tex` | `application/x-tex`, `application/x-latex` | `.tex`, `.latex` | — | Single group for TeX/LaTeX                           |
| `text/x-typst` | — | `.typ`, `.typst` | — | no established type; using naming convention         |
| `application/x-jats+xml` | — | `.jats` | — | no established type; xml parent via suffix rule    |
| `application/x-ipynb+json` | — | `.ipynb` | — | Jupyter's de-facto name; json parent via suffix rule |

#### Publishing

| Primary | Aliases | Extensions | Parent | Notes                                                                                                    |
| --- | --- | --- | --- |----------------------------------------------------------------------------------------------------------|
| `application/docbook+xml` | `application/x-docbook+xml` | `.docbook`, `.dbk`, `.docbook4`, `.docbook5` | — | shared-mime-info flips primary/alias between versions. One group — DocBook 4/5 are versions, not formats |
| `text/x-opml+xml` | `text/x-opml` | `.opml` | — | no registration |

#### Extension Preference Orders

Only one extension in scope maps to multiple groups:

| Extension | Preference order |
| --- | --- |
| `.webm` | `video/webm`, then `audio/webm` |
