using aberaTech.Fitness.Data;
using NodaTime;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// Half a year of one athlete's log, written by arithmetic rather than by a
/// random generator so that every run of the tests sees the same athlete.
/// Inserted oldest first, so a query with no ORDER BY reads it in the same
/// order as one that sorts by start time.
/// </summary>
internal static class FitnessSeed
{
    public static Task AthleteAsync(FitnessDbContext database)
    {
        var now = FitnessApp.Now;
        var today = now.InUtc().Date;

        database.Settings.Add(new AthleteSettings
        {
            Id = 1,
            ReferenceHr = 150,
            LtHr = 172,
            LtSecondsPerKm = 270,
            PlanMinutesPerWeek = 240,
            StartVdot = 44,
            VdotMeasuredOn = today.PlusDays(-40),
            BirthYear = 1992,
            Female = false,
            SustainedWeeklyHours = 6,
            PastPeakDistanceMeters = 3218.688,
            PastPeakSeconds = 750,
            PastPeakYear = 2019,
            PastPeakWeightKg = 78,
            HomeAltitudeMeters = 300,
            SelectionDate = today.PlusWeeks(30)
        });

        for (var day = 182; day >= 1; day--)
        {
            var start = now.Minus(Duration.FromDays(day)).Plus(Duration.FromHours(day % 5));

            switch (day % 7)
            {
                case 0:
                case 2:
                    database.Activities.Add(SteadyRun(day, start));
                    break;
                case 4:
                    database.Activities.Add(day % 28 == 4 ? FieldTest(day, start) : Intervals(day, start));
                    break;
                case 5:
                    database.Activities.Add(Ruck(day, start));
                    break;
                case 1:
                case 3:
                    database.Activities.Add(Strength(day, start));
                    break;
            }
        }

        for (var week = 26; week >= 0; week--)
        {
            database.BodyMetrics.Add(new BodyMetric
            {
                Id = Id(7, week),
                Date = today.PlusWeeks(-week),
                WeightKg = 84 - (26 - week) * 0.1,
                BodyFatPercent = week % 2 == 0 ? 17 - (26 - week) * 0.08 : null
            });
        }

        database.AftResults.Add(new AftResult
        {
            Id = Id(8, 1),
            Date = today.PlusDays(-150),
            DeadliftKg = 120,
            HandReleasePushUps = 38,
            SprintDragCarrySeconds = 105,
            PlankSeconds = 170,
            TwoMileSeconds = 960
        });
        database.AftResults.Add(new AftResult
        {
            Id = Id(8, 2),
            Date = today.PlusDays(-30),
            DeadliftKg = 140,
            HandReleasePushUps = 45,
            SprintDragCarrySeconds = 98,
            PlankSeconds = 200,
            TwoMileSeconds = 900
        });

        database.Predictions.Add(new LockedPrediction
        {
            Id = Id(9, 1),
            MadeOn = today.PlusDays(-90),
            TargetDate = today.PlusDays(-30),
            DistanceMeters = 3218.688,
            PredictedSeconds = 910,
            PredictedFastSeconds = 880,
            PredictedSlowSeconds = 940,
            WeeklyHours = 5,
            Compliance = 0.9,
            ActualSeconds = 900
        });
        database.Predictions.Add(new LockedPrediction
        {
            Id = Id(9, 2),
            MadeOn = today.PlusDays(-10),
            TargetDate = today.PlusDays(50),
            DistanceMeters = 8046.72,
            PredictedSeconds = 2400,
            PredictedFastSeconds = 2330,
            PredictedSlowSeconds = 2470,
            WeeklyHours = 6,
            Compliance = 0.85
        });

        return Task.CompletedTask;
    }

    private static Activity SteadyRun(int day, Instant start)
    {
        // Fitter as the log approaches today: the same heart rate, a quicker pace.
        var secPerKm = 330 + day * 0.25 + day % 3 * 2;
        var km = 8 + day % 4;
        var activity = Run(day, start, "Easy run", km * 1000, km * secPerKm, 146 + day % 6, indoor: day % 21 == 0);

        for (var lap = 0; lap < km; lap++)
        {
            activity.Laps.Add(new Lap
            {
                Id = Id(2, day * 100 + lap),
                ActivityId = activity.Id,
                Index = lap,
                DistanceMeters = 1000,
                Seconds = secPerKm + (lap % 2 == 0 ? 1.5 : -1.5),
                AverageHr = 144 + day % 6 + lap % 3
            });
        }

        return activity;
    }

    private static Activity Intervals(int day, Instant start)
    {
        var activity = Run(day, start, "6 x 800", 9600, 3000, 163, indoor: false);

        for (var lap = 0; lap < 12; lap++)
        {
            var hard = lap % 2 == 1;
            activity.Laps.Add(new Lap
            {
                Id = Id(2, day * 100 + lap),
                ActivityId = activity.Id,
                Index = lap,
                DistanceMeters = 800,
                Seconds = hard ? 190 : 310,
                AverageHr = hard ? 178 : 150
            });
        }

        return activity;
    }

    private static Activity FieldTest(int day, Instant start)
    {
        // Thirty minutes all out, the classic threshold test.
        var activity = Run(day, start, "30 min time trial", 6600 + (182 - day) * 2, 1800, 174, indoor: false);

        for (var lap = 0; lap < 6; lap++)
        {
            activity.Laps.Add(new Lap
            {
                Id = Id(2, day * 100 + lap),
                ActivityId = activity.Id,
                Index = lap,
                DistanceMeters = activity.DistanceMeters / 6,
                Seconds = 300,
                AverageHr = 168 + lap * 2
            });
        }

        return activity;
    }

    private static Activity Run(int day, Instant start, string name, double meters, double seconds, int hr, bool indoor) =>
        new()
        {
            Id = Id(1, day),
            Source = "garmin-fit",
            ExternalId = $"run-{day}",
            StartedAt = start,
            Sport = "run",
            Name = name,
            DistanceMeters = meters,
            DurationSeconds = seconds,
            AverageHr = hr,
            MaxHr = hr + 12,
            Indoor = indoor
        };

    private static Activity Ruck(int day, Instant start)
    {
        // One timed twelve-mile inside the measurement window; the rest are training marches.
        var timed = day == 47;
        var meters = timed ? 19400 : 8000 + day % 5 * 1000;

        return new Activity
        {
            Id = Id(1, day),
            Source = "garmin-fit",
            ExternalId = $"ruck-{day}",
            StartedAt = start,
            Sport = "ruck",
            Name = timed ? "12 mile" : "Ruck",
            DistanceMeters = meters,
            DurationSeconds = meters / 1000.0 * (timed ? 560 : 600 + day * 0.2),
            AverageHr = 138 + day % 7,
            // A few were never given a load, as happens.
            LoadKg = day % 35 == 5 && !timed ? null : timed ? 21 : 16 + day % 3 * 2.5
        };
    }

    private static Activity Strength(int day, Instant start)
    {
        var activity = new Activity
        {
            Id = Id(1, day),
            Source = "hevy",
            ExternalId = $"lift-{day}",
            StartedAt = start,
            Sport = "strength",
            Name = "Strength",
            DurationSeconds = 3300
        };

        var progress = (182 - day) * 0.12;
        var index = 0;

        void Add(string exercise, double kg, int reps, double? seconds = null, double? meters = null) =>
            activity.Sets.Add(new StrengthSet
            {
                Id = Id(3, day * 100 + index),
                ActivityId = activity.Id,
                Exercise = exercise,
                SetIndex = index++,
                WeightKg = kg,
                Reps = reps,
                DurationSeconds = seconds,
                DistanceMeters = meters
            });

        if (day % 7 == 1)
        {
            Add("Deadlift (Barbell)", 120 + progress, 5);
            Add("Deadlift (Barbell)", 130 + progress, 3);
            Add("Romanian Deadlift (Barbell)", 90, 8);
            Add("Bench Press (Barbell)", 80 + progress / 2, 5);
            Add("Incline Bench Press (Dumbbell)", 30, 10);
            Add("Pull Up", 0, 12 + (182 - day) / 30);
            Add("Plank", 0, 0, seconds: 150 + (182 - day) / 3.0);
            Add("Farmer's Walk", 40, 0, meters: 60 + (182 - day) / 4.0);
        }
        else
        {
            Add("Front Squat (Barbell)", 85 + progress, 3);
            Add("Squat (Barbell)", 105 + progress, 5);
            Add("Bulgarian Split Squat", 24, 10);
            Add("Push Up", 0, 40 + (182 - day) / 20);
            Add("Hand Release Push Up", 0, 30 + (182 - day) / 25);
            Add("Sit Up", 0, 55);
            // Fifteen reps is past what Epley is trusted for.
            Add("Squat (Barbell)", 60, 15);
        }

        return activity;
    }

    /// <summary>Identifiers that are the same every run, so the payload is too.</summary>
    internal static Guid Id(int kind, int number) =>
        new($"{kind:00000000}-0000-4000-8000-{number:000000000000}");
}
