namespace Luminous.InSign.Tests.InSignTriangulation;

/// <summary>
/// Geometry of TestData/flat-contract.pdf, shared between <see cref="FlatContractPdfGenerator" />
/// (which paints the document) and <see cref="CoordinateBasedSigningSessionTests" /> (which places
/// inSign signature fields on it). All values are in PDF points with the origin at the bottom-left
/// page corner; inSign's PagePosition uses values normalized to 0..1 with the origin at the
/// top-left corner.
/// </summary>
public static class FlatContractGeometry
{
    // A4 page size.
    public const double PageWidth = 595.28;
    public const double PageHeight = 841.89;

    // The two signature boxes at the bottom of the page.
    public const double BoxWidth = 200.0;
    public const double BoxHeight = 80.0;
    public const double BoxBottomY = 120.0;
    public const double LicensorBoxX = 57.0;
    public const double LicenseeBoxX = 338.28;
}
