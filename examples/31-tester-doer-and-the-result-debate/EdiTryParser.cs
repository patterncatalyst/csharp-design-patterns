namespace DeclarationParsing;

/// <summary>
/// The Framework Design Guidelines side of parsing: a <c>Try...</c> method
/// that reports an expected failure, malformed EDI text, through its boolean
/// return and never through an exception. An EDI message here is one segment
/// per line, <c>Tag*Element1*Element2</c>; the first segment's tag is taken
/// as the message type.
/// </summary>
public static class EdiTryParser
{
    public static bool TryParse(string raw, out EdiMessage? message)
    {
        ArgumentNullException.ThrowIfNull(raw);

        message = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var lines = raw.Trim().Split('\n');
        var segments = new List<EdiSegment>(lines.Length);

        foreach (var line in lines)
        {
            if (!TryParseSegment(line, out var segment) || segment is null)
            {
                return false;
            }

            segments.Add(segment);
        }

        message = new EdiMessage(segments[0].Tag, segments);
        return true;
    }

    /// <summary>
    /// Parses one <c>Tag*Element1*Element2</c> line. A line with no tag
    /// before its first element, including a blank line, is malformed.
    /// </summary>
    public static bool TryParseSegment(string line, out EdiSegment? segment)
    {
        ArgumentNullException.ThrowIfNull(line);

        segment = null;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var parts = line.Split('*');
        var tag = parts[0].Trim();
        if (tag.Length == 0)
        {
            return false;
        }

        var elements = new string[parts.Length - 1];
        for (var i = 1; i < parts.Length; i++)
        {
            elements[i - 1] = parts[i].Trim();
        }

        segment = new EdiSegment(tag, elements);
        return true;
    }
}
