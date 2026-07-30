using System.Text;

// Shared text helpers. Static — call as TextFormat.ToSpriteDigits(...); not a component.
public static class TextFormat
{
    // Converts a formatted number string (e.g. "12.50") into TMP sprite tags — one per character — for the
    // sprite-based number font: 0-9 -> <sprite=0..9>, '.' -> <sprite=10>, ',' -> <sprite=11>. Any other
    // character passes through unchanged. e.g. "12.50" -> "<sprite=1><sprite=2><sprite=10><sprite=5><sprite=0>".
    public static string ToSpriteDigits(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        StringBuilder sb = new StringBuilder(s.Length * 11);
        foreach (char c in s)
        {
            if (c >= '0' && c <= '9') sb.Append("<sprite=").Append(c - '0').Append('>');
            else if (c == '.') sb.Append("<sprite=10>");
            else if (c == ',') sb.Append("<sprite=11>");
            else sb.Append(c);
        }
        return sb.ToString();
    }
}
