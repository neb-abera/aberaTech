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
        "Eric Banister and colleagues, the exercise scientists who founded systems modeling of training; extended by Thierry Busso.",
        "A systems model of training for athletic performance (Aust J Sports Med 7); Busso, Variable dose-response relationship (Med Sci Sports Exerc 35)",
        1975,
        "https://pubmed.ncbi.nlm.nih.gov/12783044/");

    public static readonly Citation SeilerPolarized = new(
        "seiler-polarized",
        "Elite endurance athletes across sports do ~80% of sessions at low intensity; sustainable volume, not intensity, drives aerobic development.",
        "Stephen Seiler, PhD, University of Agder, is the physiologist who documented the 80/20 intensity distribution in elite endurance sport.",
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
        "Kirk Cureton, PhD, and Phillip Sparling, PhD, are exercise physiologists whose added-load experiments isolated body mass's effect on running.",
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
        "Inigo Mujika, PhD, with Sabino Padilla, PhD. Mujika is the physiologist of detraining and tapering, and coach and consultant to Olympic and world-champion endurance athletes.",
        "Detraining: loss of training-induced physiological and performance adaptations, Parts I & II (Sports Med 30)",
        2000,
        "https://pubmed.ncbi.nlm.nih.gov/10999420/");

    public static readonly Citation MuscleMemory = new(
        "muscle-memory",
        "Muscle keeps structural and epigenetic receipts of past training. Myonuclei persist through years of detraining, so retraining starts ahead.",
        "Kristian Gundersen's Oslo lab (Bruusgaard et al.) on myonuclear permanence; Robert Seaborne and Adam Sharples on the epigenetic memory of hypertrophy.",
        "Myonuclei acquired by overload persist (PNAS 107); Human skeletal muscle possesses an epigenetic memory of hypertrophy (Sci Rep 8)",
        2010,
        "https://pubmed.ncbi.nlm.nih.gov/20713720/");

    public static readonly Citation WmaAgeGrading = new(
        "wma-age-grading",
        "Distance-running capability holds essentially flat through the early thirties, then declines roughly 0.7% per year. A twenties peak is intact at 33.",
        "World Masters Athletics age-grading factors, maintained by the WMA statistics team (the Alan Jones tables). They are the standard every masters result is scored against.",
        "WMA Age-Grading Tables (2023 factors)",
        2023,
        "https://github.com/AlanLyttonJones/Age-Grade-Tables");

    public static readonly Citation PeronnetAltitude = new(
        "peronnet-altitude",
        "Aerobic race times slow with altitude: near zero below ~600 m, roughly 2-3% at Mexico City's 2,240 m, and about 1% at El Paso's elevation.",
        "Francois Peronnet, PhD, and Guy Thibault, PhD, are the exercise physiologists whose power-duration model anchored altitude effects on the Mexico City record book.",
        "A theoretical analysis of the effect of altitude on running performance (J Appl Physiol 70)",
        1991,
        "https://pubmed.ncbi.nlm.nih.gov/2010409/");

    public static readonly Citation CogganPmc = new(
        "coggan-training-load",
        "Chronic and acute training load (CTL/ATL) summarize the dose the body is adapting to; ramping chronic load gradually is what the body absorbs.",
        "Andrew Coggan, PhD, and Hunter Allen are the physiologist-coach pair behind the performance-management model most endurance platforms implement.",
        "Training and Racing with a Power Meter (VeloPress)",
        2010,
        null);

    public static readonly Citation RonnestadStrength = new(
        "ronnestad-strength",
        "Heavy resistance training improves running economy by a few percent in trained endurance athletes without adding bulk. That is a small but real ceiling gain per hour.",
        "Bent Ronnestad, PhD, Inland Norway University, and Inigo Mujika, PhD, are the pair whose review consolidated the strength-for-endurance evidence.",
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
        "Kent Pandolf, Baruch Givoni, and Ralph Goldman of the US Army Research Institute of Environmental Medicine. Theirs is the load-carriage model the Army's own research still starts from.",
        "Predicting energy expenditure with loads while standing or walking very slowly (J Appl Physiol 43)",
        1977,
        "https://pubmed.ncbi.nlm.nih.gov/908672/");

    public static readonly Citation FarinaSfasPredictors = new(
        "farina-sfas-predictors",
        "In 800 candidates, road-march speed was the strongest physical predictor of selection (top quartile selected at 66-67%, bottom at 5-6%), ahead of land navigation, run time, fitness-test score and pull-ups.",
        "Emily Farina, Lauren Thompson, Joseph Knapik, Stefan Pasiakos, James McClung, and Harris Lieberman are the US Army Research Institute of Environmental Medicine team that followed a cohort through Special Forces Assessment and Selection.",
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
        "Headquarters, Department of the Army: the scoring scales as published, approved 15 May 2025.",
        "Army Fitness Test Score Tables, effective 1 June 2025",
        2025,
        "https://www.army.mil/e2/downloads/rv7/aft/AFT_Scoring_Scales_250601.pdf");

    public static readonly Citation SfasCompetitive = new(
        "sfas-competitive",
        "Competitive at selection means a five-mile at or under 35:00, twelve miles at 45 lb at or under 2:45, twelve or more pull-ups and a fitness test well above the floor; the minimums are the price of entry, not the target.",
        "Battle Bunker's SFAS calculator and Building the Elite's selection preparation guide, both maintained by former Special Forces cadre and coaches.",
        "Army SFAS Calculator; How to prepare for Special Forces Assessment and Selection",
        2026,
        "https://thebattlebunker.com/pages/army-sfas-calculator");

    public static readonly Citation EvokeSfasReady = new(
        "evoke-sfas-ready",
        "Ready for SFAS, per Evoke's coaches. Kuenzle: \"I would get your AeT pace at least below an 8:00 mile on the flat\", a line Johnston repeats. Kuenzle attributes the lifts to Vince Paikowski: a 350 lb deadlift triple and a 250 lb squat for five. The candidate brought the five-mile in 35:00, 15 strict pull-ups, and 80 push-ups as the standard, and the coaches trained toward it. Kuenzle: the best ruckers he ever saw never trained with a ruck and were incredible runners.",
        "Jack Kuenzle (ex-Navy SEAL, FKT record holder) and Scott Johnston, answering an SFAS candidate on the Evoke Endurance forum, January–June 2023; Vince Paikowski's lifting standards as Kuenzle reports them.",
        "Army Special Forces Selection (SFAS), Evoke Endurance forum thread",
        2023,
        "https://evokeendurance.com/forums/topic/army-special-forces-selection-sfas/");

    public static readonly Citation MtiGrip = new(
        "mti-grip",
        "No tactical selection publishes a grip standard; of the grip tests tried on tactical athletes, the loaded farmer's carry is the practical grip-endurance measure, and grip's exact role in tactical performance is still unestablished.",
        "Rob Shaul's Mountain Tactical Institute, from their own lab study of farmer's carry, towel pull-up and dynamometer tests on tactical athletes.",
        "Grip strength: does it matter? How to test it. How to train it.",
        2019,
        "https://mtntactical.com/research/grip-strength-matter-test-train/");

    public static readonly Citation NebFitnessGoals = new(
        "neb-fitness-goals",
        "The athlete's own end-of-2027 goals: the essential rows of his workbook, read from it at request time, in its groups and order.",
        "Neb Abera, the athlete, in the workbook the console is scored against.",
        "physical fitness goals.xlsx (OneDrive, Documents/physical fitness)",
        2026,
        null);

    public static readonly Citation FbiPft = new(
        "fbi-pft",
        "The FBI special agent Physical Fitness Test scores pull-ups, a 300-meter sprint, untimed push-ups and a 1.5-mile run from -2 to 10 points each; 10 points takes 20 pull-ups, 40.9 s, 71 push-ups and 8:59 for men.",
        "Federal Bureau of Investigation, Special Agent Selection System: the scoring tables as published.",
        "Physical Requirements: Events 1-4 (fbijobs.gov)",
        2026,
        "https://fbijobs.gov/special-agents/physical-requirements");

    public static readonly Citation UphillAthleteHrDrift = new(
        "uphill-athlete-hr-drift",
        "On a run of an hour at a fixed heart rate, pace-to-heart-rate drift of 5% or less puts that heart rate at or below the aerobic threshold; more puts it above.",
        "Scott Johnston, Uphill Athlete: the field protocol the Evoke/Uphill Athlete method sets aerobic-threshold heart rate by.",
        "Heart Rate Drift: A Functional Measure of Aerobic Fitness (Uphill Athlete)",
        2019,
        "https://uphillathlete.com/aerobic-training/heart-rate-drift/");

    public static readonly Citation FrielLthr = new(
        "friel-lthr",
        "Lactate-threshold heart rate is the average of the final 20 minutes of a 30-minute solo time trial; pace-to-heart-rate decoupling above about 5% marks an effort past the aerobic threshold.",
        "Joe Friel, endurance coach for four decades and the author of the zone system most heart-rate training software adopted.",
        "Total Heart Rate Training (Ulysses Press); The Triathlete's Training Bible (VeloPress)",
        2006,
        "https://www.trainingpeaks.com/blog/joe-friel-s-quick-guide-to-setting-zones/");

    public static readonly Citation MaffetoneMaf = new(
        "maffetone-maf",
        "Pace at a fixed aerobic heart rate, retested monthly on the same course (the MAF test), is the field measure of aerobic development.",
        "Phil Maffetone, DC, coach of six-time Ironman world champion Mark Allen.",
        "The Big Book of Endurance Training and Racing (Skyhorse)",
        2010,
        "https://philmaffetone.com/maf-test/");

    public static readonly Citation FosterMonotony = new(
        "foster-monotony",
        "Training monotony (mean daily load over its spread) above about 2.0, and the strain it multiplies into, preceded illness and overtraining even at modest volumes.",
        "Carl Foster, PhD, University of Wisconsin–La Crosse, is the originator of session-RPE load monitoring and its monotony and strain indices.",
        "Monitoring training in athletes with reference to overtraining syndrome (Med Sci Sports Exerc 30)",
        1998,
        "https://pubmed.ncbi.nlm.nih.gov/9662690/");

    public static readonly Citation AcsmRunningEquation = new(
        "acsm-running-equation",
        "Running costs VO2 = 0.2·speed + 0.9·speed·grade + 3.5 mL/kg/min, so a treadmill incline converts to the flat pace of the same cost.",
        "American College of Sports Medicine, the professional body that sets exercise-testing standards; its metabolic equations are the standard in exercise physiology.",
        "ACSM's Guidelines for Exercise Testing and Prescription, metabolic calculations; stated for steady running faster than 134 m/min (quoted in Kokkinos et al., J Hum Kinet 2013)",
        1975,
        "https://pmc.ncbi.nlm.nih.gov/articles/PMC3743617/");

    public static readonly Citation JonesDoustTreadmillGrade = new(
        "jones-doust-treadmill-grade",
        "A 1% treadmill grade matches the energy cost of level outdoor running between 2.92 and 5.0 m/s, because the belt removes air resistance.",
        "Andrew Jones and Jonathan Doust, exercise physiologists who measured oxygen uptake in nine trained runners on the road and at five treadmill grades.",
        "A 1% treadmill grade most accurately reflects the energetic cost of outdoor running (J Sports Sci 14(4):321-327)",
        1996,
        "https://pubmed.ncbi.nlm.nih.gov/8887211/");

    public static readonly Citation LatellaStrength = new(
        "latella-strength",
        "Raw powerlifting totals of 1,271 men rose 0.15 kg a day, about 10.7% a year, over up to 15 years. The weakest quartile, at 4.57 times bodyweight, gained 0.211 kg a day on a first total of 448.6 kg, which is 17.2% a year. The strongest gained 0.102 kg a day. The rate is each lifter's best total less his first, over his days competing.",
        "Christopher Latella, Daniel van den Hoek, and colleagues at Edith Cowan University, from Australian powerlifting records 2003 to 2018.",
        "Long-term strength adaptation: a 15-year analysis of powerlifting athletes (J Strength Cond Res 34(9):2412-2418)",
        2020,
        "https://pubmed.ncbi.nlm.nih.gov/32865942/");

    public static readonly Citation LatellaTimeCourse = new(
        "latella-time-course",
        "Powerlifters gained most in their first year of competing, about 7.5 to 12.5%, and up to about 20% after 10 years.",
        "Christopher Latella, Patrick Owen, and colleagues, from the competition records of powerlifters by age class and weight class.",
        "Using powerlifting athletes to determine strength adaptations across ages in males and females: a longitudinal growth modelling approach (Sports Med 54(3):753-774)",
        2024,
        "https://pubmed.ncbi.nlm.nih.gov/38060089/");

    public static readonly Citation SteeleBeginners = new(
        "steele-beginners",
        "Adults training once a week gained about 30 to 50% in strength over their first year, and 50 to 60% of where they started after six. By one to two years the gain had nearly flattened. Leg press gained fastest and chest press slowest.",
        "James Steele, James Fisher, and colleagues, from the training records of 14,690 members of a Dutch gym chain.",
        "Long-term time-course of strength adaptation to minimal dose resistance training through retrospective longitudinal growth modelling (Res Q Exerc Sport 94(4):913-930)",
        2023,
        "https://doi.org/10.1080/02701367.2022.2070592");

    public static readonly Citation VanDenHoekNorms = new(
        "van-den-hoek-norms",
        "Among drug-tested, unequipped male powerlifters aged 18 to 35, the 10th percentile lifts 1.75 times bodyweight in the squat, 1.19 in the bench press, and 2.03 in the deadlift. The median lifts 2.28, 1.56, and 2.63, and the 90th percentile 2.83, 1.96, and 3.25.",
        "Daniel van den Hoek and colleagues, from 809,986 competition entries.",
        "Normative data for the squat, bench press and deadlift exercises in powerlifting: data from 809,986 competition entries (J Sci Med Sport 27(10):734-742)",
        2024,
        "https://doi.org/10.1016/j.jsams.2024.07.005");

    public static readonly Citation SchumannConcurrent = new(
        "schumann-concurrent",
        "Adding aerobic training to strength training did not reduce maximal strength: standardized mean difference −0.06 (95% CI −0.20 to 0.09) across 43 studies. Explosive strength fell, −0.28.",
        "Moritz Schumann and colleagues, an updated systematic review and meta-analysis.",
        "Compatibility of concurrent aerobic and strength training for skeletal muscle size and function: an updated systematic review and meta-analysis (Sports Med 52(3):601-612)",
        2022,
        "https://pmc.ncbi.nlm.nih.gov/articles/PMC8891239/");

    public static readonly Citation MurphyDeficit = new(
        "murphy-deficit",
        "Resistance training in an energy deficit gained less lean mass (effect size −0.57, p = 0.02) and as much strength (−0.31, p = 0.28) as training without one.",
        "Chaise Murphy and Karsten Koehler, a meta-analysis and meta-regression of randomized trials.",
        "Energy deficiency impairs resistance training gains in lean mass but not strength: a meta-analysis and meta-regression (Scand J Med Sci Sports 32(1):125-137)",
        2022,
        "https://pubmed.ncbi.nlm.nih.gov/34623696/");

    public static readonly Citation ThomasWeightLoss = new(
        "thomas-weight-loss",
        "Dieters lost 20.1 ± 11.3 lb where the 3,500 kcal a pound rule predicted 27.6 lb. A kilogram lost held 4,858 kcal at week 4 and 6,569 kcal at week 24, against the rule's 7,700.",
        "Diana Thomas, Steven Heymsfield, and colleagues, from controlled feeding studies and the CALERIE trial.",
        "Can a weight loss of one pound a week be achieved with a 3,500 kcal deficit? Commentary on a commonly accepted rule (Int J Obes 37(12):1611-1613); Time to correctly predict the amount of weight loss with dieting (J Acad Nutr Diet 114(6):857-861)",
        2013,
        "https://pmc.ncbi.nlm.nih.gov/articles/PMC4024447/");

    public static readonly Citation HeymsfieldWeightLoss = new(
        "heymsfield-weight-loss",
        "The lean share of weight lost was 0.27 ± 0.06 on diet alone and 0.17 ± 0.14 with resistance training, in overweight adults. Ranger students at 15% body fat lost 0.40 lean over 8 weeks of extreme restriction.",
        "Steven Heymsfield and colleagues, a critical review of the rule that a quarter of weight lost is fat-free mass.",
        "Weight loss composition is one-fourth fat-free mass: a critical review and critique of this widely cited rule (Obes Rev 15(4):310-321)",
        2014,
        "https://pmc.ncbi.nlm.nih.gov/articles/PMC3970209/");

    public static readonly Citation HelanderWeighIns = new(
        "helander-weigh-ins",
        "The change between consecutive weigh-ins carried a residual standard deviation of 0.764% of bodyweight, across 2,838 weigh-ins by 40 adults.",
        "Elina Helander and colleagues, VTT Technical Research Centre of Finland.",
        "Are breaks in daily self-weighing associated with weight gain? (PLoS One 9(11):e113164)",
        2014,
        "https://pmc.ncbi.nlm.nih.gov/articles/PMC4232563/");

    public static readonly Citation FestaThresholdSpeed = new(
        "festa-threshold-speed",
        "Recreational runners gained 4.0% in speed at the first ventilatory threshold in 8 weeks of polarized training, and 3.2% on a threshold-focused plan.",
        "Luca Festa and colleagues, University of Verona.",
        "Effects of different training intensity distribution in recreational runners (Front Sports Act Living 1:70)",
        2019,
        "https://pmc.ncbi.nlm.nih.gov/articles/PMC7739641/");

    public static readonly Citation StrengthRetraining = new(
        "strength-retraining",
        "Strength lost to time off returns fast. Men who lost 5 to 15% of their 1RM in 12 weeks off regained it in under 8 weeks. After 3 weeks off, bench press gains matched the first 6 weeks of training. 24 weeks off cost 6% of 1RM.",
        "Sara Blocquiaux and colleagues (KU Leuven); Riki Ogasawara and colleagues (University of Tokyo); Keijo Häkkinen and colleagues (University of Jyväskylä).",
        "The effect of resistance training, detraining and retraining on muscle strength and power, myofibre size, satellite cells and myonuclei in older men (Exp Gerontol 133); Effects of periodic and continued resistance training on muscle CSA and strength in previously untrained men (Clin Physiol Funct Imaging 31); Neuromuscular adaptation during prolonged strength training, detraining and re-strength-training in middle-aged and elderly people (Eur J Appl Physiol 83)",
        2020,
        "https://pubmed.ncbi.nlm.nih.gov/32017951/");

    public static readonly Citation ReynoldsRepMax = new(
        "reynolds-rep-max",
        "A one-rep max estimated from a 5RM chest press carried a standard error of 2.98 kg, and linear equations hold to 10 reps.",
        "Jeff Reynolds, Timothy Gordon, and Robert Robergs, University of New Mexico.",
        "Prediction of one repetition maximum strength from multiple repetition maximum testing and anthropometry (J Strength Cond Res 20(3):584-592)",
        2006,
        "https://pubmed.ncbi.nlm.nih.gov/16937972/");

    public static IReadOnlyList<Citation> All { get; } =
    [
        DanielsVdot, BanisterModel, MujikaRetraining, MuscleMemory,
        WmaAgeGrading, PeronnetAltitude, SeilerPolarized, SanMillanBrooks,
        UphillAthleteAet, CuretonSparling, Epley, Brzycki, CogganPmc,
        RonnestadStrength, GabbettWorkload, NonlinearRegression,
        PandolfLoadCarriage, FarinaSfasPredictors, FarinaSfasBody, ArmyAft,
        SfasCompetitive,
        EvokeSfasReady, MtiGrip, NebFitnessGoals, UphillAthleteHrDrift, FrielLthr,
        MaffetoneMaf, FosterMonotony, FbiPft, AcsmRunningEquation, JonesDoustTreadmillGrade,
        LatellaStrength, LatellaTimeCourse, SchumannConcurrent, MurphyDeficit, StrengthRetraining, ReynoldsRepMax,
        ThomasWeightLoss, HeymsfieldWeightLoss, HelanderWeighIns, FestaThresholdSpeed,
        SteeleBeginners, VanDenHoekNorms
    ];
}
