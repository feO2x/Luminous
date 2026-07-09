using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using static Luminous.InSign.Tests.InSignTriangulation.FlatContractGeometry;

namespace Luminous.InSign.Tests.InSignTriangulation;

/// <summary>
/// Icebreaker integration test against the public inSign sandbox. It exercises the core Luminous
/// scenario: a flat PDF (no AcroForm fields, no ##SIG text tags) whose signature boxes are placed
/// purely via API coordinates. The created session is intentionally left open at the end so that
/// the invited signers can complete it manually via the links they receive by email.
/// </summary>
public sealed class CoordinateBasedSigningSessionTests
{
    // The sandbox returns positions rounded to five decimal places.
    private const double PositionTolerance = 0.0001;

    private const string DocumentId = "flatcontract";
    private const string PdfFileName = "flat-contract.pdf";

    private readonly ITestOutputHelper _output;

    public CoordinateBasedSigningSessionTests(ITestOutputHelper output) => _output = output;

    [Fact(Explicit = true)]
    public async Task CreateSessionWithSignatureBoxesAtCoordinates()
    {
        var settings = InSignOptions.Load();
        if (settings.LicensorEmail is null || settings.LicenseeEmail is null)
        {
            Assert.Skip(
                "Signer emails are not configured. Run 'dotnet user-secrets set \"InSign:LicensorEmail\" " +
                "<email>' and 'dotnet user-secrets set \"InSign:LicenseeEmail\" <email>' in the " +
                "Luminous.InSign.Tests project directory."
            );
        }

        using var client = CreateHttpClient(settings);

        // Step 1: create a session that declares the document with two coordinate-based signature
        // boxes but without file content - the PDF itself follows via /configure/uploaddocument.
        var createSessionResponse = await PostAsync<CreateSessionResponse>(
            client,
            "/configure/session",
            new
            {
                foruser = "luminousicebreaker",
                userFullName = "Luminous Icebreaker Test",
                displayname = "Luminous icebreaker - coordinate-based signature boxes",
                documents = new[]
                {
                    new
                    {
                        id = DocumentId,
                        displayname = "Software License Agreement",
                        mustbesigned = true,
                        scanSigTags = false,
                        signatures = new[]
                        {
                            CreateSignatureBox("sigLicensor", "Licensor", "LICENSOR", LicensorBoxX),
                            CreateSignatureBox("sigLicensee", "Licensee", "LICENSEE", LicenseeBoxX)
                        }
                    }
                }
            }
        );

        createSessionResponse.Error.Should().Be(0, createSessionResponse.Message);
        createSessionResponse.SessionId.Should().NotBeNullOrWhiteSpace();
        var sessionId = createSessionResponse.SessionId!;

        // Step 2: upload the flat PDF into the prepared document slot via multipart/form-data.
        var pdfBytes = await File.ReadAllBytesAsync(
            Path.Combine(AppContext.BaseDirectory, "InSignTriangulation", PdfFileName),
            TestContext.Current.CancellationToken
        );

        var uploadResponse = await UploadDocumentAsync(client, sessionId, pdfBytes);
        uploadResponse.Error.Should().Be(0, uploadResponse.Message);
        uploadResponse.DocId.Should().Be(DocumentId);

        // Step 3: read the document metadata back and verify the signature boxes round-tripped.
        // This must happen before /extern/beginmulti: once the session is in external mode, the
        // owner-perspective annotations report required=false and readonly=true because the
        // fields are locked to the invited signers.
        var documentsResponse = await PostAsync<DocumentsFullResponse>(
            client,
            "/get/documents/full?includeAnnotations=true",
            new { sessionid = sessionId }
        );

        documentsResponse.Error.Should().Be(0, documentsResponse.Message);
        documentsResponse.Documents.Should().HaveCount(1);
        var document = documentsResponse.Documents![0];
        document.NumberOfPages.Should().Be(1);
        document.Annotations.Should().HaveCount(2);

        VerifySignatureBox(document.Annotations!, "sigLicensor", "Licensor", "LICENSOR", LicensorBoxX);
        VerifySignatureBox(document.Annotations!, "sigLicensee", "Licensee", "LICENSEE", LicenseeBoxX);

        // Step 4: invite both signers by email; each signature box is bound to its signer via role.
        var beginExternResponse = await PostAsync<BeginExternResponse>(
            client,
            "/extern/beginmulti",
            new
            {
                sessionid = sessionId,
                inOrder = false,
                externUsers = new[]
                {
                    CreateExternSigner(settings.LicensorEmail, "LICENSOR"),
                    CreateExternSigner(settings.LicenseeEmail, "LICENSEE")
                }
            }
        );

        beginExternResponse.Error.Should().Be(0, beginExternResponse.Message);
        beginExternResponse.ExternUsers.Should().HaveCount(2);
        beginExternResponse.ExternUsers.Should().OnlyContain(user => user.Error == 0);

        _output.WriteLine($"Session {sessionId} is ready for manual signing.");
        _output.WriteLine($"Owner access URL: {createSessionResponse.AccessUrl}");
        foreach (var user in beginExternResponse.ExternUsers!)
        {
            _output.WriteLine($"Signer {user.ExternUser}: {user.ExternAccessLink}");
        }
    }

    /// <summary>
    /// Reloads an existing signing session and outputs fresh access URLs. inSign access URLs are
    /// one-time links, so this test can be re-run whenever a new link is needed. The session id
    /// must be provided via User Secrets or an environment variable, e.g.
    /// <c>dotnet user-secrets set "InSign:SessionId" &lt;id&gt;</c>.
    /// Note: the owner access URL only works while the session is NOT in external mode. Once
    /// /extern/beginmulti has been called, the session is locked to the invited signers and the
    /// owner editor rejects the link - use the extern links (also output below) instead.
    /// </summary>
    [Fact(Explicit = true)]
    public async Task OutputFreshAccessUrls()
    {
        var settings = InSignOptions.Load();
        if (settings.SessionId is null)
        {
            Assert.Skip(
                "No session id is configured. Run 'dotnet user-secrets set \"InSign:SessionId\" <id>' in " +
                "the Luminous.InSign.Tests project directory (the id is logged by " +
                $"{nameof(CreateSessionWithSignatureBoxesAtCoordinates)})."
            );
        }

        using var client = CreateHttpClient(settings);

        var loadSessionResponse = await PostAsync<CreateSessionResponse>(
            client,
            "/persistence/loadsession",
            new { sessionid = settings.SessionId }
        );

        loadSessionResponse.Error.Should().Be(0, loadSessionResponse.Message);
        loadSessionResponse.AccessUrl.Should().NotBeNullOrWhiteSpace();

        _output.WriteLine($"Session: {settings.SessionId}");
        _output.WriteLine($"Owner access URL (works only before /extern/beginmulti): {loadSessionResponse.AccessUrl}");
        _output.WriteLine($"Process management URL: {loadSessionResponse.AccessUrlProcessManagement}");

        // /extern/users is read-only; it lists the invited signers/watchers with their links.
        var externUsersResponse = await PostAsync<BeginExternResponse>(
            client,
            "/extern/users",
            new { sessionid = settings.SessionId }
        );

        if (externUsersResponse is { Error: 0, ExternUsers: { Count: > 0 } externUsers })
        {
            foreach (var user in externUsers)
            {
                _output.WriteLine($"Extern link for {user.ExternUser}: {user.ExternAccessLink}");
            }
        }
    }

    private static object CreateSignatureBox(string id, string displayName, string role, double boxXInPoints) =>
        new
        {
            id,
            displayname = displayName,
            // inSign authorizes an external signer for a field by matching the signature's "role"
            // against the signer's "roles". Setting only "externRole" leaves "role" null: a single
            // signer then still gets every field, but as soon as several signers are invited, no
            // field is assigned to anyone and signing fails in the browser with
            // "Unterschrift in diesem Feld für Nutzer nicht erlaubt". So "role" is the load-bearing
            // property here; "externRole" is kept because it drives the external editing behaviour.
            role,
            externRole = role,
            required = true,
            // The sandbox's default signature level is AES, which requires a touch device, the
            // inSign app, or SMS pairing and therefore fails in a plain desktop browser. SES
            // allows drawing the signature with the mouse; the legal level is irrelevant for
            // this test - it only verifies signature box placement. Note: the session-level
            // signatureLevel property is ignored by the sandbox, so it must be set per signature.
            signatureLevel = "SES",
            position = new
            {
                page = 0,
                x0 = boxXInPoints / PageWidth,
                y0 = (PageHeight - BoxBottomY - BoxHeight) / PageHeight,
                w = BoxWidth / PageWidth,
                h = BoxHeight / PageHeight
            }
        };

    private static object CreateExternSigner(string email, string role) =>
        new
        {
            recipient = email,
            roles = new[] { role },
            roletype = "signer",
            sendEmails = true,
            subject = "Luminous icebreaker test - please sign the sample contract",
            note = $"You were invited as {role} by the Luminous integration test."
        };

    private static void VerifySignatureBox(
        IReadOnlyList<AnnotationResponse> annotations,
        string id,
        string displayName,
        string role,
        double boxXInPoints
    )
    {
        var annotation = annotations.Should()
           .ContainSingle(a => a.Id == id)
           .Which;
        annotation.Type.Should().Be("signature_marker");
        annotation.DisplayName.Should().Be(displayName);
        // "role" binds the field to the invited signer - without it, signing fails once several
        // signers are invited.
        annotation.Role.Should().Be(role);
        annotation.ExternRole.Should().Be(role);
        annotation.Required.Should().BeTrue();
        annotation.Position.Should().NotBeNull();
        annotation.Position!.Page.Should().Be(0);
        annotation.Position.X0.Should().BeApproximately(boxXInPoints / PageWidth, PositionTolerance);
        annotation.Position.Y0.Should()
           .BeApproximately((PageHeight - BoxBottomY - BoxHeight) / PageHeight, PositionTolerance);
        annotation.Position.W.Should().BeApproximately(BoxWidth / PageWidth, PositionTolerance);
        annotation.Position.H.Should().BeApproximately(BoxHeight / PageHeight, PositionTolerance);
    }

    private static HttpClient CreateHttpClient(InSignOptions settings)
    {
        var client = new HttpClient { BaseAddress = new Uri(settings.BaseUrl) };
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{settings.UserName}:{settings.Password}")
        );
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        return client;
    }

    private Task<T> PostAsync<T>(HttpClient client, string path, object requestBody) =>
        SendAsync<T>(client, path, JsonContent.Create(requestBody));

    private Task<UploadDocumentResponse> UploadDocumentAsync(HttpClient client, string sessionId, byte[] pdfBytes)
    {
        var multipartContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        multipartContent.Add(fileContent, "file", PdfFileName);

        // The endpoint expects the document metadata as query parameters; only the file content
        // itself travels in the multipart body.
        var path = $"/configure/uploaddocument?sessionid={Uri.EscapeDataString(sessionId)}" +
                   $"&filename={Uri.EscapeDataString(PdfFileName)}&docid={Uri.EscapeDataString(DocumentId)}";
        return SendAsync<UploadDocumentResponse>(client, path, multipartContent);
    }

    private async Task<T> SendAsync<T>(HttpClient client, string path, HttpContent requestContent)
    {
        using (requestContent)
        {
            using var response = await client.PostAsync(path, requestContent, TestContext.Current.CancellationToken);
            var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            _output.WriteLine($"POST {path} -> {(int) response.StatusCode} {response.StatusCode}");
            response.IsSuccessStatusCode.Should().BeTrue($"POST {path} failed with body: {content}");
            var result = JsonSerializer.Deserialize<T>(content, JsonSerializerOptions.Web);
            result.Should().NotBeNull();
            return result;
        }
    }
}
