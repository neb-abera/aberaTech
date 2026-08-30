namespace aberaTech.Fitness.Ingest;

/// <summary>
/// A minimal RFC 4180 reader: quoted fields, embedded commas and quotes,
/// nothing else. Thirty lines beats a dependency for two fixed, well-formed
/// export formats.
/// </summary>
internal static class Csv
{
    public static List<string[]> Parse(string text)
    {
        var rows = new List<string[]>();
        var field = new System.Text.StringBuilder();
        var row = new List<string>();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (inQuotes)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') inQuotes = false;
                else field.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == ',') { row.Add(field.ToString()); field.Clear(); }
            else if (c is '\n' or '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(field.ToString());
                field.Clear();
                if (row.Count > 1 || row[0].Length > 0) rows.Add([.. row]);
                row.Clear();
            }
            else field.Append(c);
        }

        row.Add(field.ToString());
        if (row.Count > 1 || row[0].Length > 0) rows.Add([.. row]);

        return rows;
    }
}
