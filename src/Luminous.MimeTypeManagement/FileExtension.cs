using System;

namespace Luminous.MimeTypeManagement;

public readonly struct FileExtension : IEquatable<FileExtension>
{
    private readonly string? _value;

    private FileExtension(string value)
    {
        _value = value;
    }

    public string Value => _value ?? string.Empty;

    public bool IsDefault => _value is null;

    public static FileExtension Parse(ReadOnlySpan<char> value)
    {
        if (!TryParse(value, out var extension))
        {
            throw new FormatException("The value is not a valid file extension.");
        }

        return extension;
    }

    public static bool TryParse(ReadOnlySpan<char> value, out FileExtension extension)
    {
        extension = default;

        if (value.StartsWith("*.", StringComparison.Ordinal))
        {
            value = value[2..];
        }
        else if (!value.IsEmpty && value[0] == '.')
        {
            value = value[1..];
        }

        if (value.IsEmpty || value[0] == '.' || value[^1] == '.')
        {
            return false;
        }

        var previousWasDot = false;
        foreach (var character in value)
        {
            if (character == '.')
            {
                if (previousWasDot)
                {
                    return false;
                }

                previousWasDot = true;
                continue;
            }

            previousWasDot = false;
            if (!IsAsciiAlphaNumeric(character) && character is not '-' and not '_' and not '+')
            {
                return false;
            }
        }

        extension = new FileExtension(string.Concat(".", value.ToString().ToLowerInvariant()));
        return true;
    }

    public bool Equals(FileExtension other)
    {
        return string.Equals(_value, other._value, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return obj is FileExtension other && Equals(other);
    }

    public override int GetHashCode()
    {
        return _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
    }

    public override string ToString()
    {
        return Value;
    }

    public static bool operator ==(FileExtension left, FileExtension right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(FileExtension left, FileExtension right)
    {
        return !left.Equals(right);
    }

    private static bool IsAsciiAlphaNumeric(char value)
    {
        return value is >= '0' and <= '9' or >= 'A' and <= 'Z' or >= 'a' and <= 'z';
    }
}
