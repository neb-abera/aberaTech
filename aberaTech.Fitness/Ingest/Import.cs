using System.IO.Compression;
using System.Text;
using aberaTech.Fitness.Data;
using NodaTime;

namespace aberaTech.Fitness.Ingest;

/// <summary>What one upload turned out to be, and what came out of it.</summary>
public sealed record ImportResult(string Kind, IReadOnlyList<Activity> Activities);

/// <summary>
/// One door for every file these services actually hand out.
///
/// The page used to ask which of three buttons a file belonged to, which meant
/// knowing that Garmin's "Export Your Data" archive is not the same thing as
/// the Connect website's activities CSV — and the archive, the thing Garmin
/// emails you, fitted none of the three. So nothing is asked: the bytes are
/// sniffed, an archive is walked, and whatever is recognised inside it is
/// imported.
///
/// Content decides, never the file name. A name is a claim the uploader makes;
/// the magic numbers are what the file is.
/// </summary>
public static class Import
{
    /// <summary>Entries in one archive. A Garmin export of many years is a few thousand.</summary>
    private const int MaxEntries = 20_000;

    /// <summary>Any one member, decompressed. The largest thing expected is the summaries JSON.</summary>
    private const long MaxEntryBytes = 64L * 1024 * 1024;

    /// <summary>Everything decompressed, across the whole walk — the zip-bomb ceiling.</summary>
    private const long MaxTotalBytes = 512L * 1024 * 1024;

    /// <summary>Garmin nests originals in a second zip. Nothing legitimate goes deeper.</summary>
    private const int MaxDepth = 2;

    public static ImportResult Read(Stream file, DateTimeZone csvZone)
    {
        using var buffer = new MemoryStream();
        file.CopyTo(buffer);

        var collected = new Collected();
        var budget = new Budget();
        ReadBytes(buffer.ToArray(), csvZone, collected, budget, depth: 0);

        var activities = collected.Merge();
        if (activities.Count == 0)
        {
            throw new FormatException(
                "Nothing importable in that file. Expected a Garmin export archive, a Garmin activities CSV, a .fit file, or a Hevy export CSV.");
        }

        return new ImportResult(collected.Describe(), activities);
    }

    /// <summary>
    /// One row per activity, however many files in the archive described it.
    ///
    /// A Garmin export says the same run twice: once in the summaries JSON and
    /// again as the original .fit under uploaded files. Both clocks are real
    /// UTC, so matching on the start minute — two activities never begin in the
    /// same one — lets the .fit fill in what the summary left out, instead of
    /// the archive importing every run of the year twice.
    /// </summary>
    internal static IReadOnlyList<Activity> Merge(
        IReadOnlyList<Activity> summaries,
        IReadOnlyList<Activity> fits,
        bool fromArchive)
    {
        var byMinute = new Dictionary<long, Activity>();
        foreach (var summary in summaries)
        {
            byMinute[Minute(summary)] = summary;
        }

        foreach (var fit in fits)
        {
            if (byMinute.TryGetValue(Minute(fit), out var summary))
            {
                summary.DistanceMeters ??= fit.DistanceMeters;
                summary.AverageHr ??= fit.AverageHr;
                summary.MaxHr ??= fit.MaxHr;
                continue;
            }

            // An activity the summaries do not cover — an older one, or a file
            // uploaded to Connect from elsewhere. Keyed by its start, so
            // re-importing the same archive lands on the same row.
            if (fromArchive)
            {
                fit.Source = "garmin-export";
                fit.ExternalId = $"garmin:fit:{fit.StartedAt.ToUnixTimeSeconds()}";
            }

            byMinute[Minute(fit)] = fit;
        }

        return [.. byMinute.Values];
    }

    private static long Minute(Activity activity) => activity.StartedAt.ToUnixTimeSeconds() / 60;

    private static void ReadBytes(byte[] bytes, DateTimeZone csvZone, Collected collected, Budget budget, int depth)
    {
        if (bytes.Length == 0) return;

        if (IsZip(bytes))
        {
            if (depth >= MaxDepth) return;
            ReadArchive(bytes, csvZone, collected, budget, depth);
            return;
        }

        if (IsFit(bytes))
        {
            using var stream = new MemoryStream(bytes, writable: false);
            var activity = FitImport.Parse(stream);
            // A Garmin archive carries settings and workout .fit files beside
            // the activities; those decode fine and have no session, so a null
            // here is ordinary rather than an error.
            if (activity is not null) collected.Fits.Add(activity);
            return;
        }

        var text = Text(bytes);
        if (text is null) return;

        if (GarminExportJson.Looks(text))
        {
            collected.Summaries.AddRange(GarminExportJson.Parse(text));
            return;
        }

        ReadCsv(text, csvZone, collected);
    }

    private static void ReadCsv(string text, DateTimeZone csvZone, Collected collected)
    {
        var header = text[..Math.Min(text.Length, 2048)].ToLowerInvariant();

        // Each export names itself in its header row, so the choice is read off
        // the file rather than guessed from the extension both of them share.
        if (header.Contains("exercise_title") || header.Contains("start_time"))
        {
            collected.Others.AddRange(HevyCsv.Parse(text, csvZone));
        }
        else if (header.Contains("activity type"))
        {
            collected.Others.AddRange(GarminActivitiesCsv.Parse(text, csvZone));
        }
    }

    private static void ReadArchive(byte[] bytes, DateTimeZone csvZone, Collected collected, Budget budget, int depth)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        collected.Archive = true;

        var seen = 0;
        foreach (var entry in archive.Entries)
        {
            if (++seen > MaxEntries) break;

            // Directory markers, and anything whose declared size alone is
            // already past what a member may weigh.
            if (entry.Length == 0 || entry.Length > MaxEntryBytes) continue;

            byte[] member;
            try
            {
                member = budget.Read(entry);
            }
            catch (BudgetExceeded)
            {
                // The archive is claiming more than a real export can hold.
                // Stop walking rather than keep feeding it.
                break;
            }
            catch (InvalidDataException)
            {
                continue; // One corrupt member should not lose the rest.
            }

            ReadBytes(member, csvZone, collected, budget, depth + 1);
        }
    }

    /// <summary>The local file header of every zip, PKZip's own signature.</summary>
    private static bool IsZip(byte[] bytes) =>
        bytes.Length >= 4 && bytes[0] == 'P' && bytes[1] == 'K' && bytes[2] == 3 && bytes[3] == 4;

    /// <summary>A FIT file carries ".FIT" at offset 8, after its header length and profile fields.</summary>
    private static bool IsFit(byte[] bytes) =>
        bytes.Length >= 12
        && bytes[8] == '.' && bytes[9] == 'F' && bytes[10] == 'I' && bytes[11] == 'T';

    /// <summary>Text, or null when the bytes are some binary this does not read.</summary>
    private static string? Text(byte[] bytes)
    {
        // A NUL in the first kilobyte is the cheap, reliable tell for binary;
        // none of these exports is UTF-16.
        var probe = Math.Min(bytes.Length, 1024);
        for (var i = 0; i < probe; i++)
        {
            if (bytes[i] == 0) return null;
        }

        return new UTF8Encoding(false, throwOnInvalidBytes: false).GetString(bytes).TrimStart('\uFEFF');
    }

    private sealed class BudgetExceeded : Exception
    {
    }

    /// <summary>
    /// How many decompressed bytes the whole walk is allowed. A zip's declared
    /// sizes are written by whoever made it, so the count is of bytes actually
    /// read, and the read stops the moment the ceiling is crossed.
    /// </summary>
    private sealed class Budget
    {
        private long _spent;

        public byte[] Read(ZipArchiveEntry entry)
        {
            using var source = entry.Open();
            using var destination = new MemoryStream();

            var chunk = new byte[81920];
            int read;
            while ((read = source.Read(chunk, 0, chunk.Length)) > 0)
            {
                _spent += read;
                if (_spent > MaxTotalBytes || destination.Length + read > MaxEntryBytes)
                {
                    throw new BudgetExceeded();
                }

                destination.Write(chunk, 0, read);
            }

            return destination.ToArray();
        }
    }

    private sealed class Collected
    {
        public bool Archive { get; set; }
        public List<Activity> Summaries { get; } = [];
        public List<Activity> Fits { get; } = [];
        public List<Activity> Others { get; } = [];

        /// <summary>
        /// One row per activity, however many files described it.
        ///
        /// An export describes the same run twice: once in the summaries JSON
        /// and again as an original .fit under uploaded files. Matching them on
        /// the start minute — two activities never begin in the same one — lets
        /// the .fit fill in what the summary left out, instead of the archive
        /// importing every run of the year twice.
        /// </summary>
        public IReadOnlyList<Activity> Merge() =>
            [.. Import.Merge(Summaries, Fits, fromArchive: Archive), .. Others];

        public string Describe()
        {
            if (Archive) return "Garmin export archive";
            if (Summaries.Count > 0) return "Garmin activity summaries";
            if (Fits.Count > 0) return "Garmin .fit activity";
            return "export CSV";
        }
    }
}
