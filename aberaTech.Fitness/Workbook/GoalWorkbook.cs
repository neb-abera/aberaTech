using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace aberaTech.Fitness.Workbook;

/// <summary>One cell as the sheet holds it: a number, or text such as "untested", or nothing.</summary>
public sealed record WorkbookCell(double? Number, string? Text)
{
    public static readonly WorkbookCell Empty = new(null, null);

    public bool IsEmpty => Number is null && string.IsNullOrWhiteSpace(Text);
}

/// <summary>
/// One essential row of the goals workbook, read as the athlete wrote it.
/// </summary>
/// <param name="Row">The sheet's row number, so a finding can point at the cell.</param>
/// <param name="Group">The label in column A that opened this row's group ("Run calculator", "AFT 428 on 04APR26", "FBI", "Body comp").</param>
/// <param name="Label">Column B, the exercise.</param>
/// <param name="Multiple">A number in column A: the bodyweight multiple the row's goal is set at.</param>
/// <param name="Baseline">Column C.</param>
/// <param name="Ideal">Column D.</param>
/// <param name="Goal">Column F, the end-of-2027 goal.</param>
/// <param name="Evidence">Column I, the athlete's note on where the numbers came from.</param>
public sealed record WorkbookRow(
    int Row,
    string Group,
    string Label,
    double? Multiple,
    WorkbookCell Baseline,
    WorkbookCell Ideal,
    WorkbookCell Goal,
    string? Evidence);

/// <summary>The essential rows of the goals sheet, in the sheet's order.</summary>
/// <param name="SheetName">The first sheet's name.</param>
/// <param name="GoalYear">The year column F's header names ("End of year goal (2027)"); the goals fall due on 31 December of it.</param>
/// <param name="Rows">The essential rows.</param>
public sealed record GoalSheet(string SheetName, int? GoalYear, IReadOnlyList<WorkbookRow> Rows);

/// <summary>Why a workbook could not be read; the page shows the reason.</summary>
public sealed class GoalWorkbookException(string message) : Exception(message);

/// <summary>
/// Reads the athlete's own goals workbook, <c>physical fitness goals.xlsx</c>.
/// </summary>
/// <remarks>
/// The layout is the athlete's, and it is the specification: column A opens a
/// group with a label or carries a bodyweight multiple, column B names the
/// exercise, C is the baseline, D the ideal, F the end-of-2027 goal and I the
/// evidence. The header row is the one whose column B says "Exercise". The
/// essential rows end where column A says "Non-essential"; nothing below that
/// line is read. Times are Excel day fractions and are read as numbers here;
/// the goal catalog knows which rows are times. Cached formula results are
/// read as values, which is what Excel displays.
/// </remarks>
public static class GoalWorkbook
{
    /// <summary>A goals sheet is kilobytes; anything near this is not one.</summary>
    public const long MaxBytes = 5 * 1024 * 1024;

    /// <summary>Rows read before giving up on finding the essential section's end.</summary>
    public const int MaxRows = 500;

    private const string HeaderLabel = "Exercise";
    private const string EndMarker = "non-essential";

    public static GoalSheet Parse(Stream xlsx)
    {
        ArgumentNullException.ThrowIfNull(xlsx);

        using var copy = new MemoryStream();
        CopyBounded(xlsx, copy);
        copy.Position = 0;

        SpreadsheetDocument document;
        try
        {
            document = SpreadsheetDocument.Open(copy, isEditable: false);
        }
        catch (Exception e) when (e is not GoalWorkbookException)
        {
            throw new GoalWorkbookException("The file is not an Excel workbook.");
        }

        using (document)
        {
            try
            {
                return Read(document);
            }
            catch (Exception e) when (e is not GoalWorkbookException and not OutOfMemoryException)
            {
                // Parts load lazily, so a damaged part surfaces here as the
                // SDK's own exception. Whatever it was, the file cannot be read.
                throw new GoalWorkbookException("The workbook is damaged and could not be read.");
            }
        }
    }

    private static GoalSheet Read(SpreadsheetDocument document)
    {
        var workbookPart = document.WorkbookPart
                           ?? throw new GoalWorkbookException("The workbook has no sheets.");
        var sheet = workbookPart.Workbook?.Sheets?.Elements<Sheet>().FirstOrDefault()
                    ?? throw new GoalWorkbookException("The workbook has no sheets.");
        if (sheet.Id?.Value is not { } id || workbookPart.GetPartById(id) is not WorksheetPart worksheetPart)
        {
            throw new GoalWorkbookException("The first sheet could not be opened.");
        }

        var strings = workbookPart.SharedStringTablePart?.SharedStringTable?
            .Elements<SharedStringItem>()
            .Select(item => item.InnerText)
            .ToArray() ?? [];

        var cells = ReadCells(worksheetPart, strings);
        var (year, rows) = Essential(cells);
        return new GoalSheet(sheet.Name?.Value ?? "", year, rows);
    }

    private static void CopyBounded(Stream source, Stream target)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += read;
            if (total > MaxBytes) throw new GoalWorkbookException("The file is larger than a goals workbook can be.");
            target.Write(buffer, 0, read);
        }
    }

    /// <summary>Every non-empty cell of the first sheet, by row and column letter.</summary>
    private static SortedDictionary<int, Dictionary<string, WorkbookCell>> ReadCells(
        WorksheetPart worksheetPart, string[] strings)
    {
        var rows = new SortedDictionary<int, Dictionary<string, WorkbookCell>>();
        var data = worksheetPart.Worksheet?.GetFirstChild<SheetData>();
        if (data is null) return rows;

        foreach (var row in data.Elements<Row>())
        {
            if (row.RowIndex?.Value is not { } index || index > MaxRows) continue;

            var values = new Dictionary<string, WorkbookCell>();
            foreach (var cell in row.Elements<Cell>())
            {
                if (cell.CellReference?.Value is not { } reference) continue;
                var column = new string(reference.TakeWhile(char.IsLetter).ToArray());
                var value = Value(cell, strings);
                if (!value.IsEmpty) values[column] = value;
            }

            if (values.Count > 0) rows[(int)index] = values;
        }

        return rows;
    }

    private static WorkbookCell Value(Cell cell, string[] strings)
    {
        var raw = cell.CellValue?.Text;
        var type = cell.DataType?.Value;

        if (type == CellValues.SharedString)
        {
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) && i >= 0 && i < strings.Length
                ? new WorkbookCell(null, strings[i].Trim())
                : WorkbookCell.Empty;
        }

        if (type == CellValues.InlineString)
        {
            return new WorkbookCell(null, cell.InlineString?.InnerText.Trim());
        }

        if (type == CellValues.String || type == CellValues.Error)
        {
            // A formula's text result, or #VALUE!: text, never a number.
            return new WorkbookCell(null, raw?.Trim());
        }

        if (type == CellValues.Boolean || string.IsNullOrWhiteSpace(raw)) return WorkbookCell.Empty;

        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number)
            ? new WorkbookCell(number, null)
            : new WorkbookCell(null, raw.Trim());
    }

    /// <summary>The rows between the header and the non-essential line, grouped by column A.</summary>
    private static (int? GoalYear, IReadOnlyList<WorkbookRow> Rows) Essential(SortedDictionary<int, Dictionary<string, WorkbookCell>> rows)
    {
        var header = rows.FirstOrDefault(r =>
            r.Value.TryGetValue("B", out var b) && string.Equals(b.Text, HeaderLabel, StringComparison.OrdinalIgnoreCase));
        if (header.Value is null) throw new GoalWorkbookException("No header row: column B never says \"Exercise\".");

        int? goalYear = header.Value.GetValueOrDefault("F")?.Text is { } goalHeader
                        && System.Text.RegularExpressions.Regex.Match(goalHeader, @"\b(20\d\d)\b") is { Success: true } match
            ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)
            : null;

        var result = new List<WorkbookRow>();
        var group = "";
        var ended = false;

        foreach (var (index, cells) in rows.Where(r => r.Key > header.Key))
        {
            var a = cells.GetValueOrDefault("A") ?? WorkbookCell.Empty;

            if (a.Text is { } text && text.Contains(EndMarker, StringComparison.OrdinalIgnoreCase))
            {
                ended = true;
                break;
            }

            if (!string.IsNullOrWhiteSpace(a.Text)) group = a.Text.Trim();

            if (cells.GetValueOrDefault("B")?.Text is not { Length: > 0 } label) continue;

            result.Add(new WorkbookRow(
                index,
                group,
                label,
                a.Number,
                cells.GetValueOrDefault("C") ?? WorkbookCell.Empty,
                cells.GetValueOrDefault("D") ?? WorkbookCell.Empty,
                cells.GetValueOrDefault("F") ?? WorkbookCell.Empty,
                cells.GetValueOrDefault("I")?.Text));
        }

        if (!ended) throw new GoalWorkbookException("No \"Non-essential metrics\" line in column A, so the essential rows have no end.");
        if (result.Count == 0) throw new GoalWorkbookException("No essential rows between the header and the non-essential line.");

        return (goalYear, result);
    }
}
