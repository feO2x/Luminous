using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Luminous.InSign.Tests.InSignTriangulation;

// Response DTOs for the inSign REST API, limited to the members the tests assert on.
// The corresponding OpenAPI schemas can be found at https://sandbox.test.getinsign.show/v3/api-docs
// (SessionResult, ExternMultiuserResult, ExternUserResult, SessionData, DocumentData, Annotation,
// and PagePosition; /configure/uploaddocument declares text/html in the spec but actually returns
// JSON). Deserialization uses JsonSerializerOptions.Web, so the all-lowercase JSON
// member names (e.g. "sessionid") bind case-insensitively to the C# properties.

// ReSharper disable ClassNeverInstantiated.Global -- instantiated via deserialization
public sealed record CreateSessionResponse(
    int Error,
    string? Message,
    [property: JsonPropertyName("sessionid")] string? SessionId,
    [property: JsonPropertyName("accessURL")] string? AccessUrl,
    [property: JsonPropertyName("accessURLProcessManagement")] string? AccessUrlProcessManagement
);

public sealed record UploadDocumentResponse(
    int Error,
    string? Message,
    [property: JsonPropertyName("docid")] string? DocId
);

public sealed record BeginExternResponse(
    int Error,
    string? Message,
    List<ExternUserResponse>? ExternUsers
);

public sealed record ExternUserResponse(
    int Error,
    string? Message,
    string? ExternUser,
    string? ExternAccessLink
);

public sealed record DocumentsFullResponse(
    int Error,
    string? Message,
    List<DocumentResponse>? Documents
);

public sealed record DocumentResponse(
    [property: JsonPropertyName("docid")] string? DocId,
    [property: JsonPropertyName("numberofpages")] int NumberOfPages,
    List<AnnotationResponse>? Annotations
);

public sealed record AnnotationResponse(
    string? Id,
    string? Type,
    [property: JsonPropertyName("displayname")] string? DisplayName,
    string? Role,
    string? ExternRole,
    bool Required,
    PositionResponse? Position
);

public sealed record PositionResponse(int Page, double X0, double Y0, double W, double H);
// ReSharper restore ClassNeverInstantiated.Global
