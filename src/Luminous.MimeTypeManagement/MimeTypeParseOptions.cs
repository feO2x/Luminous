using System;

namespace Luminous.MimeTypeManagement;

public sealed class MimeTypeParseOptions
{
    public const int RfcMaxNameLength = 127;

    public static MimeTypeParseOptions Default { get; } = new ();

    public int MaxNameLength
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            field = value;
        }
    } = RfcMaxNameLength;
}
