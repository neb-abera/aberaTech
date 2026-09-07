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

    public static readonly Citation RonnestadStrength = new(
        "ronnestad-strength",
        "Heavy resistance training improves running economy by a few percent in trained endurance athletes without adding bulk — a small but real ceiling gain per hour.",
        "Bent Ronnestad, PhD, Inland Norway University, and Inigo Mujika, PhD — the pair whose review consolidated the strength-for-endurance evidence.",
        "Optimizing strength training for running and cycling endurance performance: a review (Scand J Med Sci Sports 24)",
        2014,
        "https://pubmed.ncbi.nlm.nih.gov/24151913/");

    public static readonly Citation GabbettWorkload = new(
        "gabbett-workload",
        "Injury risk rises when the training load of the current week runs far ahead of the load the athlete has been carrying; chronic load is built, not jumped to.",
        "Tim Gabbett, PhD - applied sport scientist whose acute:chronic workload work underpins how professional sport ramps training.",
        "The training-injury prevention paradox (Br J Sports Med 50)",
        2016,
        "https://pubmed.ncbi.nlm.nih.gov/26758673/");

    public static readonly Citation NonlinearRegression = new(
        "seber-wild-nls",
        "Fitting a nonlinear model to observations by least squares yields parameter standard errors from the Jacobian, and prediction intervals by propagating them.",
        "George Seber and Chris Wild, statisticians, University of Auckland - the standard reference on nonlinear regression.",
        "Nonlinear Regression (Wiley Series in Probability and Statistics)",
        1989,
        null);

    public static readonly Citation PandolfLoadCarriage = new(
        "pandolf-load-carriage",
        "The metabolic cost of walking under load is M = 1.5W + 2.0(W+L)(L/W)² + η(W+L)(1.5V² + 0.35VG) watts; it under-predicts once the gait breaks into a shuffle above ~2.2 m/s.",
        "Kent Pandolf, Baruch Givoni and Ralph Goldman of the US Army Research Institute of Environmental Medicine — the load-carriage model the Army's own research still starts from.",
        "Predicting energy expenditure with loads while standing or walking very slowly (J Appl Physiol 43)",
        1977,
        "https://pubmed.ncbi.nlm.nih.gov/908672/");

    public static readonly Citation FarinaSfasPredictors = new(
        "farina-sfas-predictors",
        "In 800 candidates, road-march speed was the strongest physical predictor of selection (top quartile selected at 66-67%, bottom at 5-6%), ahead of land navigation, run time, fitness-test score and pull-ups.",
        "Emily Farina, Lauren Thompson, Joseph Knapik, Stefan Pasiakos, James McClung and Harris Lieberman — the US Army Research Institute of Environmental Medicine team that followed a cohort through Special Forces Assessment and Selection.",
        "Physical performance, demographic, psychological, and physiological predictors of success in the U.S. Army Special Forces Assessment and Selection course (Physiol Behav 210)",
        2019,
        "https://pubmed.ncbi.nlm.nih.gov/31401079/");

    public static readonly Citation FarinaSfasBody = new(
        "farina-sfas-body",
        "In the same cohort the leanest quartile (~14% body fat) was selected at 51.6% and the fattest (~25%) at 13.8%; the highest lean-mass quartile (~73 kg) at 58.6% and the lowest (~54 kg) at 20%.",
        "Emily Farina and the USARIEM team, on the anthropometrics of the same 800-candidate cohort.",
        "Anthropometrics and Body Composition Predict Physical Performance and Selection to Attend Special Forces Training in United States Army Soldiers (Mil Med 187)",
        2022,
        "https://academic.oup.com/milmed/article/187/11-12/1381/6327577");

    public static readonly Citation ArmyAft = new(
        "army-aft-2025",
        "The Army Fitness Test scores five events 0-100 by age band; combat specialties need 60 in every event and 350 in total, on the sex-neutral scale.",
        "Headquarters, Department of the Army — the scoring scales as published, approved 15 May 2025.",
        "Army Fitness Test Score Tables, effective 1 June 2025",
        2025,
        "https://www.army.mil/e2/downloads/rv7/aft/AFT_Scoring_Scales_250601.pdf");

    public static readonly Citation SfasDayOne = new(
        "sfas-day-one",
        "The day-one assessment at Special Forces Assessment and Selection drops anyone under 28 hand-release push-ups, 6 pull-ups or a 15:12 two-mile; sub-13:32, 30+, 12+ and a sub-15:00/mile ruck are what selected candidates look like.",
        "Task & Purpose's reporting of the standards published by the 1st Special Warfare Training Group, as briefed to candidates.",
        "Special Forces Assessment and Selection: everything you need to know",
        2024,
        "https://taskandpurpose.com/military-life/special-forces-assessment-selection-green-beret/");

    public static readonly Citation EvokeSelectionPrep = new(
        "evoke-selection-prep",
        "The entry test for the final fifteen-week selection block: 12 miles at 35 lb under 3:00, a two-mile under 14:00, five miles under 45:00, 40 hand-release push-ups, 10 pull-ups, a bodyweight front-squat triple and a 1.5× bodyweight deadlift triple.",
        "Evoke Endurance's military coaching team — Vince Paikowski, Jack Kuenzle and Scott Johnston — the plan this athlete is training on.",
        "Selection Prep 2.0 training plan",
        2025,
        "https://evokeendurance.com/training-plans/selection-prep-2-0/");

    public static readonly Citation GuardSfrePrerequisites = new(
        "guard-sfre-prereqs",
        "A National Guard Special Forces company's readiness evaluation expects a 13:42 two-mile, 64 push-ups and 72 sit-ups in two minutes, 12 miles at 45 lb dry under three hours, pull-ups, a rope climb and a 100 m swim.",
        "Company C, 1st Battalion, 19th Special Forces Group (California Army National Guard) — one of the units that gates the Guard's SFAS slots with this evaluation.",
        "Special Forces Readiness Evaluation flyer",
        2022,
        "https://calguard.ca.gov/wp-content/uploads/sites/62/2022/07/SFRE_FlyerSepNov2022Updated.pdf");

    public static readonly Citation SfasCompetitive = new(
        "sfas-competitive",
        "Competitive at selection means a five-mile at or under 35:00, twelve miles at 45 lb at or under 2:45, twelve or more pull-ups and a fitness test well above the floor; the minimums are the price of entry, not the target.",
        "Battle Bunker's SFAS calculator and Building the Elite's selection preparation guide, both maintained by former Special Forces cadre and coaches.",
        "Army SFAS Calculator; How to prepare for Special Forces Assessment and Selection",
        2026,
        "https://thebattlebunker.com/pages/army-sfas-calculator");

    public static IReadOnlyList<Citation> All { get; } =
    [
        DanielsVdot, BanisterModel, MujikaRetraining, MuscleMemory,
        WmaAgeGrading, PeronnetAltitude, SeilerPolarized, SanMillanBrooks,
        UphillAthleteAet, CuretonSparling, Epley, Brzycki, CogganPmc,
        RonnestadStrength, GabbettWorkload, NonlinearRegression,
        PandolfLoadCarriage, FarinaSfasPredictors, FarinaSfasBody, ArmyAft,
        SfasDayOne, EvokeSelectionPrep, GuardSfrePrerequisites, SfasCompetitive
    ];
}
