using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// A goals workbook laid out the way the athlete's is: title in row 1, the
/// header in row 2, groups opened by a label in column A, bodyweight
/// multiples as numbers in column A, times as fractions of a day, a
/// "Non-essential metrics" line, and rows below it that must not be read.
/// </summary>
/// <remarks>
/// The values are the shape of his, not his: the repository is public and the
/// workbook holds health data. Strings go through the shared string table,
/// the way Excel writes them.
/// </remarks>
internal static class WorkbookFixture
{
    public static double Time(int hours, int minutes, double seconds) => (hours * 3600 + minutes * 60 + seconds) / 86400.0;

    /// <summary>One row: column letter to value (string or double).</summary>
    public sealed record Line(int Row, IReadOnlyDictionary<string, object> Cells);

    public static IReadOnlyList<Line> Standard() =>
    [
        new(1, Cells(("A", "Physical fitness goals (double click to edit bodyweight, exit to recalculate)"))),
        new(2, Cells(("B", "Exercise"), ("C", "Baseline"), ("D", "Ideal"), ("E", "Difference"), ("F", "End of year goal (2027)"), ("G", "Difference from goal"), ("H", "Verified current (Aug 2026)"), ("I", "Evidence (Hevy / Garmin / TP)"))),
        new(3, Cells(("A", "Run calculator"), ("B", "5 mile run"), ("C", Time(0, 39, 0)), ("D", Time(0, 30, 0)), ("F", Time(0, 34, 0)), ("I", "retest"))),
        new(4, Cells(("B", "10 mile run"), ("C", Time(2, 0, 55)), ("F", Time(1, 10, 54)), ("I", "inferred from the 5-mile"))),
        new(5, Cells(("B", "12mi ruck @ 35lbs"), ("C", Time(3, 0, 0)), ("D", Time(2, 15, 0)), ("F", Time(2, 30, 0)))),
        new(6, Cells(("B", "Aerobic threshold pace (flat, per mile)"), ("C", Time(0, 10, 30)), ("D", Time(0, 8, 0)), ("F", Time(0, 8, 0)))),
        new(7, Cells(("A", "AFT 428 on 04APR26"), ("B", "Max 3 rep deadlift"), ("C", 300.0), ("D", 350.0), ("F", 350.0))),
        new(8, Cells(("B", "HR pushups"), ("C", 44.0), ("D", 62.0), ("F", 62.0))),
        new(9, Cells(("B", "sprint drag carry"), ("C", Time(0, 1, 44)), ("D", Time(0, 1, 30)), ("F", Time(0, 1, 30)))),
        new(10, Cells(("B", "plank"), ("C", Time(0, 2, 0)), ("D", Time(0, 3, 30)), ("F", Time(0, 3, 30)))),
        new(11, Cells(("B", "2 mile run"), ("C", Time(0, 16, 49)), ("D", Time(0, 13, 25)), ("F", Time(0, 12, 15)))),
        new(12, Cells(("A", "FBI"), ("B", "Pull-ups"), ("C", 6.0), ("D", 20.0), ("F", 15.0))),
        new(13, Cells(("B", "300m sprint"), ("C", "untested"), ("D", Time(0, 0, 41)), ("E", "#VALUE!"), ("F", Time(0, 0, 40.9)))),
        new(14, Cells(("B", "Push-ups"), ("C", 25.0), ("D", 75.0), ("F", 80.0))),
        new(15, Cells(("B", "1.5m run"), ("C", "untested"), ("D", Time(0, 8, 59)), ("F", Time(0, 8, 59)))),
        new(16, Cells(("A", "Body comp"), ("B", "Bodyweight"), ("C", 174.0), ("D", 165.0), ("F", 165.0))),
        new(17, Cells(("B", "Body fat %"), ("C", 15.0), ("D", 10.0), ("F", 10.0))),
        new(18, Cells(("A", 1.5), ("B", "Bench Press"), ("C", 215.0), ("D", 247.5), ("F", 247.5))),
        new(19, Cells(("A", 1.5), ("B", "Front Squat"), ("C", 202.0), ("D", 247.5), ("F", 247.5))),
        new(20, Cells(("B", "Farmer carry 1.5x bw m"), ("C", 25.0), ("D", 100.0))),
        new(21, Cells(("A", 2.0), ("B", "Back Squat"), ("C", 225.0), ("D", 348.0), ("F", 315.0))),
        new(22, Cells(("A", "Non-essential metrics"))),
        new(23, Cells(("A", 1.25), ("B", "Clean"), ("C", 175.0), ("D", 206.25), ("F", 206.25))),
        new(27, Cells(("B", "500y Swim"), ("C", "untested"), ("F", 145.0)))
    ];

    public static Dictionary<string, object> Cells(params (string Column, object Value)[] cells) =>
        cells.ToDictionary(c => c.Column, c => c.Value);

    public static byte[] Build(IReadOnlyList<Line>? lines = null, string sheetName = "goals")
    {
        lines ??= Standard();
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new DocumentFormat.OpenXml.Spreadsheet.Workbook();

            var strings = new List<string>();
            var data = new SheetData();
            foreach (var line in lines.OrderBy(l => l.Row))
            {
                var row = new Row { RowIndex = (uint)line.Row };
                foreach (var (column, value) in line.Cells.OrderBy(c => c.Key, StringComparer.Ordinal))
                {
                    var cell = new Cell { CellReference = $"{column}{line.Row}" };
                    if (value is string text)
                    {
                        var index = strings.IndexOf(text);
                        if (index < 0)
                        {
                            strings.Add(text);
                            index = strings.Count - 1;
                        }

                        cell.DataType = CellValues.SharedString;
                        cell.CellValue = new CellValue(index.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        cell.CellValue = new CellValue(Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture));
                    }

                    row.Append(cell);
                }

                data.Append(row);
            }

            var sharedStrings = workbookPart.AddNewPart<SharedStringTablePart>();
            sharedStrings.SharedStringTable = new SharedStringTable(strings.Select(s => new SharedStringItem(new Text(s))));

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = new Worksheet(data);

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(worksheetPart), SheetId = 1, Name = sheetName });
            workbookPart.Workbook.Save();
        }

        return stream.ToArray();
    }

    public static IReadOnlyList<Line> Without(int row) => Standard().Where(l => l.Row != row).ToArray();

    public static IReadOnlyList<Line> With(params Line[] extra) => Standard().Concat(extra).ToArray();
}
