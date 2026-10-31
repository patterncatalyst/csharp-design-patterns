namespace DeclarationParsing;

/// <summary>
/// The <see cref="Result{T}"/> side of parsing the same EDI text that
/// <see cref="EdiTryParser"/> parses: instead of a boolean and an
/// <c>out</c> parameter, a caller gets back a value that carries either the
/// parsed message or the specific reason parsing failed.
/// </summary>
public static class EdiResultParser
{
    public static Result<EdiMessage> Parse(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        if (string.IsNullOrWhiteSpace(raw))
        {
            return Result<EdiMessage>.Failure("The EDI message is empty.");
        }

        var lines = raw.Trim().Split('\n');
        var segments = new List<EdiSegment>(lines.Length);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return Result<EdiMessage>.Failure("A segment line is blank.");
            }

            var parts = line.Split('*');
            var tag = parts[0].Trim();
            if (tag.Length == 0)
            {
                return Result<EdiMessage>.Failure($"Segment '{line}' has no tag.");
            }

            var elements = new string[parts.Length - 1];
            for (var i = 1; i < parts.Length; i++)
            {
                elements[i - 1] = parts[i].Trim();
            }

            segments.Add(new EdiSegment(tag, elements));
        }

        return Result<EdiMessage>.Success(new EdiMessage(segments[0].Tag, segments));
    }
}
