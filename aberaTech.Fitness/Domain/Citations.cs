namespace aberaTech.Fitness.Domain;

/// <summary>One sourced claim the engine relies on.</summary>
/// <param name="Id">Stable key the API and UI reference.</param>
/// <param name="Claim">What the model asserts, in one sentence.</param>
/// <param name="Who">The people behind it and why they are credible on it.</param>
/// <param name="Work">The publication or body of work.</param>
/// <param name="Year">First publication year.</param>
/// <param name="Url">Where to read it, when it is freely readable.</param>
public sealed record Citation(string Id, string Claim, string Who, string Work, int Year, string? Url);

/// <summary>
/// Every model in this library, sourced. Discipline-matched on purpose: running
/// claims cite running coaches and physiologists, strength claims cite
/// strength researchers.
/// </summary>
public static class Citations
{
    public static readonly Citation DanielsVdot = new(
        "daniels-vdot",
        "A race time implies an oxygen-cost score (VDOT); equal VDOT means equivalent performances across distances.",
        "Jack Daniels, PhD exercise physiology, two-time Olympic medalist, named 'world's best running coach' by Runner's World; with J. Roy Gilbert.",
        "Oxygen Power: Performance Tables for Distance Runners; Daniels' Running Formula (Human Kinetics)",
        1979,
        "https://www.humankinetics.com/products/daniels-running-formula-4th-edition");

    public static readonly Citation BanisterModel = new(
        "banister-impulse-response",
        "Fitness rises toward a dose-dependent ceiling, fast at first and slower as the gap closes (impulse-response training model).",
        "Eric Banister and colleagues, the exercise scientists who founded systems modelling of training; extended by Thierry Busso.",
        "A systems model of training for athletic performance (Aust J Sports Med 7); Busso, Variable dose-response relationship (Med Sci Sports Exerc 35)",
        1975,
        "https://pubmed.ncbi.nlm.nih.gov/12783044/");

    public static readonly Citation SeilerPolarized = new(
        "seiler-polarized",
        "Elite endurance athletes across sports do ~80% of sessions at low intensity; sustainable volume, not intensity, drives aerobic development.",
        "Stephen Seiler, PhD, University of Agder — the physiologist who documented the 80/20 intensity distribution in elite endurance sport.",
        "What is best practice for training intensity and duration distribution in endurance athletes? (Int J Sports Physiol Perform 5)",
        2010,
        "https://pubmed.ncbi.nlm.nih.gov/20861519/");

    public static readonly Citation SanMillanBrooks = new(
        "san-millan-zone2",
        "Low-intensity (Zone 2) training builds mitochondrial capacity and lactate clearance; deficits there mark the undertrained aerobic system.",
        "Iñigo San-Millán, PhD (coach to Tour de France winner Tadej Pogačar) and George Brooks, PhD, UC Berkeley, author of the lactate-shuttle theory.",
        "Assessment of metabolic flexibility by means of measuring blood lactate, fat, and carbohydrate oxidation (Front Physiol / Sports Med)",
        2018,
        "https://pubmed.ncbi.nlm.nih.gov/29910237/");

    public static readonly Citation UphillAthleteAet = new(
        "uphill-athlete-aet",
        "When aerobic-threshold pace lags lactate-threshold pace by more than ~10%, the athlete is aerobically deficient and base volume is the fix.",
        "Scott Johnston (coach of Olympic and world-champion endurance athletes) with Steve House; applied to military athletes with Jack Kuenzle (ex-Navy SEAL, FKT record holder) and Vince Paikowski (US Army SOF, 2021 Best Ranger winner, D1 runner).",
        "Training for the Uphill Athlete; Training for the Military Athlete (Evoke Endurance)",
        2019,
        "https://evokeendurance.com/training-for-the-military-athlete/");

    public static readonly Citation CuretonSparling = new(
        "cureton-added-mass",
        "Relative VO2max and distance-running performance scale with the inverse of body mass: ~1% added (or shed) fat mass moves relative VO2max ~1%.",
        "Kirk Cureton, PhD, and Phillip Sparling, PhD — exercise physiologists whose added-load experiments isolated body mass's effect on running.",
        "Distance running performance and metabolic responses to running with excess weight (Med Sci Sports Exerc 12)",
        1980,
        "https://pubmed.ncbi.nlm.nih.gov/7392900/");

    public static readonly Citation Epley = new(
        "epley-1rm",
        "One-rep max is estimable from a submaximal set: 1RM ≈ w·(1 + reps/30).",
        "Boyd Epley, founding strength coach of Nebraska's athletic performance program and first president of the NSCA.",
        "Poundage Chart, Boyd Epley Workout (University of Nebraska)",
        1985,
        null);

    public static readonly Citation Brzycki = new(
        "brzycki-1rm",
        "Cross-check estimate: 1RM ≈ w·36/(37 − reps), reliable below ten reps.",
        "Matt Brzycki, Princeton University strength and conditioning coordinator.",
        "Strength testing: predicting a one-rep max from reps-to-fatigue (J Phys Educ Recreat Dance 64)",
        1993,
        null);

    public static readonly Citation MujikaRetraining = new(
        "mujika-retraining",
        "Detrained athletes regain previously held fitness far faster than novices build it; much of detraining loss is rapidly reversible.",
        "Inigo Mujika, PhD — the physiologist of detraining and tapering, coach and consultant to Olympic and world-champion endurance athletes; with Sabino Padilla, PhD.",
        "Detraining: loss of training-induced physiological and performance adaptations, Parts I & II (Sports Med 30)",
        2000,
        "https://pubmed.ncbi.nlm.nih.gov/10999420/");

    public static readonly Citation MuscleMemory = new(
        "muscle-memory",
        "Muscle keeps structural and epigenetic receipts of past training — myonuclei persist through years of detraining — so retraining starts ahead.",
        "Kristian Gundersen's Oslo lab (Bruusgaard et al.) on myonuclear permanence; Robert Seaborne and Adam Sharples on the epigenetic memory of hypertrophy.",
        "Myonuclei acquired by overload persist (PNAS 107); Human skeletal muscle possesses an epigenetic memory of hypertrophy (Sci Rep 8)",
        2010,
        "https://pubmed.ncbi.nlm.nih.gov/20713720/");

    public static readonly Citation WmaAgeGrading = new(
        "wma-age-grading",
        "Distance-running capability holds essentially flat through the early thirties, then declines roughly 0.7% per year — so a twenties peak is intact at 33.",
        "World Masters Athletics age-grading factors, maintained by the WMA statistics team (the Alan Jones tables) — the standard every masters result is scored against.",
        "WMA Age-Grading Tables (2023 factors)",
        2023,
        "https://github.com/AlanLyttonJones/Age-Grade-Tables");

    public static readonly Citation PeronnetAltitude = new(
        "peronnet-altitude",
        "Aerobic race times slow with altitude — near zero below ~600 m, roughly 2-3% at Mexico City's 2,240 m, about 1% at El Paso's elevation.",
        "Francois Peronnet, PhD, and Guy Thibault, PhD — the exercise physiologists whose power-duration model anchored altitude effects on the Mexico City record book.",
        "A theoretical analysis of the effect of altitude on running performance (J Appl Physiol 70)",
        1991,
        "https://pubmed.ncbi.nlm.nih.gov/2010409/");

    public static readonly Citation CogganPmc = new(
        "coggan-training-load",
        "Chronic and acute training load (CTL/ATL) summarise the dose the body is adapting to; ramping chronic load gradually is what the body absorbs.",
        "Andrew Coggan, PhD, and Hunter Allen — the physiologist-coach pair behind the performance-management model most endurance platforms implement.",
        "Training and Racing with a Power Meter (VeloPress)",
        2010,
        null);

    public static IReadOnlyList<Citation> All { get; } =
    [
        DanielsVdot, BanisterModel, MujikaRetraining, MuscleMemory,
        WmaAgeGrading, PeronnetAltitude, SeilerPolarized, SanMillanBrooks,
        UphillAthleteAet, CuretonSparling, Epley, Brzycki, CogganPmc
    ];
}
