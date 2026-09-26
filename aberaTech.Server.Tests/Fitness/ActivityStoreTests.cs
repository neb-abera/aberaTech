using aberaTech.Fitness.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// The upsert's rules, and the number of statements it costs.
/// </summary>
/// <remarks>
/// The count is the point of the last test here. The rules were already met by
/// a version that asked the database once per incoming activity: two queries
/// for the same-session check and the keyed lookup, four statements each. A
/// Garmin export is a few thousand activities, so that was twelve thousand
/// round trips inside one HTTP request. Nothing about the answers changed when
/// it was batched, which is exactly why a behaviour test could not have caught
/// the regression and a counted one can.
/// </remarks>
public sealed class ActivityStoreTests : IDisposable
{
    private readonly FitnessApp _app = new();

    private static readonly Instant Start = Instant.FromUtc(2026, 3, 2, 14, 30);

    public void Dispose() => _app.Dispose();

    [Fact]
    public async Task The_same_external_id_lands_on_the_same_row_twice()
    {
        await UpsertAsync([Run("garmin-export", "a1", Start, 3600, 10_000)]);
        var second = await UpsertAsync([Run("garmin-export", "a1", Start, 3700, 10_500)]);

        Assert.Equal(0, second.Added);

        var stored = await AllAsync();
        Assert.Single(stored);
        Assert.Equal(3700, stored[0].DurationSeconds);
        Assert.Equal(10_500, stored[0].DistanceMeters);
    }

    [Fact]
    public async Task A_wall_clock_row_the_export_already_describes_is_not_stored()
    {
        await UpsertAsync([Run("garmin-export", "a1", Start, 3600, 10_000)]);

        // Nine hours out: a plausible zone offset, well inside the tolerance,
        // and the same session by duration and distance.
        var outcome = await UpsertAsync(
            [Run("garmin-csv", "c1", Start + Duration.FromHours(9), 3600, 10_010)]);

        Assert.Equal(0, outcome.Added);
        Assert.Equal(1, outcome.Skipped);
        Assert.Single(await AllAsync());
    }

    [Fact]
    public async Task An_export_supersedes_the_wall_clock_copy_already_stored()
    {
        await UpsertAsync([Run("garmin-csv", "c1", Start + Duration.FromHours(9), 3600, 10_010)]);

        var outcome = await UpsertAsync([Run("garmin-export", "a1", Start, 3600, 10_000)]);

        Assert.Equal(1, outcome.Added);
        Assert.Equal(1, outcome.Superseded);

        var stored = await AllAsync();
        Assert.Single(stored);
        Assert.Equal("garmin-export", stored[0].Source);
    }

    [Fact]
    public async Task One_stale_row_is_superseded_once_however_many_records_replace_it()
    {
        await UpsertAsync([Run("garmin-csv", "c1", Start + Duration.FromHours(9), 3600, 10_010)]);

        // Two exports of the same session, in one batch. The delete is not
        // saved until the batch ends, so a count taken per activity would say
        // two and drop one row.
        var outcome = await UpsertAsync([
            Run("garmin-export", "a1", Start, 3600, 10_000),
            Run("garmin-fit", "f1", Start, 3600, 10_000)
        ]);

        Assert.Equal(1, outcome.Superseded);
        Assert.Equal(2, outcome.Added);
        Assert.Equal(2, (await AllAsync()).Count);
    }

    [Fact]
    public async Task A_batch_carrying_one_external_id_twice_stores_one_row()
    {
        var outcome = await UpsertAsync([
            Run("garmin-export", "a1", Start, 3600, 10_000),
            Run("garmin-export", "a1", Start, 3700, 10_500)
        ]);

        Assert.Equal(1, outcome.Added);

        var stored = await AllAsync();
        Assert.Single(stored);
        Assert.Equal(3700, stored[0].DurationSeconds);
    }

    [Fact]
    public async Task A_workout_synced_again_replaces_its_sets()
    {
        // Hevy's daily sync sends every workout every time, so a stored
        // workout comes back with its sets on each run. The new sets carry
        // ids of their own, and EF Core read a set reached through a tracked
        // workout with its generated key already filled in as an existing
        // row: an UPDATE that matched nothing. From 2026-09-23 every
        // production sync failed that way (442 times in three days) and no
        // new workout landed.
        await UpsertAsync([Workout("w1", ("Squat", 100, 5), ("Squat", 105, 5))]);
        var second = await UpsertAsync([Workout("w1", ("Squat", 100, 5), ("Squat", 110, 3), ("Squat", 110, 3))]);

        Assert.Equal(0, second.Added);

        using var scope = _app.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<FitnessDbContext>();
        var stored = await database.Activities.Include(a => a.Sets).SingleAsync();
        Assert.Equal([100.0, 110.0, 110.0], stored.Sets.OrderBy(s => s.SetIndex).Select(s => s.WeightKg));
        Assert.Equal(3, await database.Set<StrengthSet>().CountAsync());
    }

    [Fact]
    public async Task A_run_imported_again_replaces_its_laps()
    {
        var first = Run("garmin-export", "a1", Start, 3600, 10_000);
        first.Laps.Add(new Lap { Id = Guid.NewGuid(), Index = 0, DistanceMeters = 1000, Seconds = 300 });
        await UpsertAsync([first]);

        var again = Run("garmin-export", "a1", Start, 3600, 10_000);
        again.Laps.Add(new Lap { Id = Guid.NewGuid(), Index = 0, DistanceMeters = 1000, Seconds = 290 });
        again.Laps.Add(new Lap { Id = Guid.NewGuid(), Index = 1, DistanceMeters = 1000, Seconds = 295 });
        await UpsertAsync([again]);

        using var scope = _app.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<FitnessDbContext>();
        var stored = await database.Activities.Include(a => a.Laps).SingleAsync();
        Assert.Equal([290.0, 295.0], stored.Laps.OrderBy(l => l.Index).Select(l => l.Seconds));
        Assert.Equal(2, await database.Set<Lap>().CountAsync());
    }

    [Fact]
    public async Task Laps_and_sets_survive_a_file_that_does_not_carry_them()
    {
        var withLaps = Run("garmin-export", "a1", Start, 3600, 10_000);
        withLaps.Laps.Add(new Lap { Id = Guid.NewGuid(), Index = 0, DistanceMeters = 1000, Seconds = 300 });

        await UpsertAsync([withLaps]);
        await UpsertAsync([Run("garmin-export", "a1", Start, 3600, 10_000)]);

        using var scope = _app.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<FitnessDbContext>();
        var stored = await database.Activities.Include(a => a.Laps).SingleAsync();

        Assert.Single(stored.Laps);
    }

    [Fact]
    public async Task The_statement_count_does_not_grow_with_the_batch()
    {
        // A log already stored, so the queries have rows to find and the
        // bisection has something to search.
        await UpsertAsync([.. Enumerable.Range(0, 40).Select(i =>
            Run("garmin-export", $"old{i}", Start.Plus(Duration.FromDays(i)), 3600, 10_000))]);

        var ten = await CountAsync(Batch(10));
        var eighty = await CountAsync(Batch(80));

        // Two reads and the save, whatever the batch holds. The save itself is
        // one statement per row on SQLite, so only the reads are compared.
        Assert.Equal(ten.Reads, eighty.Reads);
        Assert.Equal(2, ten.Reads);
    }

    private static Activity[] Batch(int count) =>
        [.. Enumerable.Range(0, count).Select(i =>
            Run("garmin-export", $"new{count}-{i}", Start.Plus(Duration.FromHours(i)), 3600, 10_000))];

    private async Task<(int Reads, int Total)> CountAsync(IReadOnlyList<Activity> batch)
    {
        _app.Commands.Reset();
        await UpsertAsync(batch);

        // SaveChanges runs inside one transaction and the counter sees every
        // statement, so the reads are what is left once the writes are taken
        // out. The two reads happen before anything is written.
        return (_app.Commands.Reads, _app.Commands.Count);
    }

    private async Task<UpsertOutcome> UpsertAsync(IReadOnlyList<Activity> batch)
    {
        using var scope = _app.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<FitnessDbContext>();
        return await ActivityStore.UpsertAsync(database, batch, CancellationToken.None);
    }

    private async Task<List<Activity>> AllAsync()
    {
        using var scope = _app.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<FitnessDbContext>();
        return await database.Activities.AsNoTracking().OrderBy(a => a.StartedAt).ToListAsync();
    }

    private static Activity Workout(string externalId, params (string Exercise, double Kg, int Reps)[] sets)
    {
        var workout = new Activity
        {
            Id = Guid.NewGuid(),
            Source = "hevy-api",
            ExternalId = externalId,
            StartedAt = Start,
            Sport = "strength",
            DurationSeconds = 3600
        };

        for (var i = 0; i < sets.Length; i++)
        {
            workout.Sets.Add(new StrengthSet
            {
                Id = Guid.NewGuid(),
                ActivityId = workout.Id,
                Exercise = sets[i].Exercise,
                SetIndex = i,
                WeightKg = sets[i].Kg,
                Reps = sets[i].Reps
            });
        }

        return workout;
    }

    private static Activity Run(
        string source, string externalId, Instant startedAt, double seconds, double metres) =>
        new()
        {
            Id = Guid.NewGuid(),
            Source = source,
            ExternalId = externalId,
            StartedAt = startedAt,
            Sport = "run",
            DurationSeconds = seconds,
            DistanceMeters = metres
        };
}
