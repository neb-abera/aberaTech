/**
 * The signal processing curriculum, as data.
 *
 * The page renders this and nothing else, so the plan can be tested as a
 * plan: every task has an address, every link is https, every block ends
 * in a gate with a number in it, every book on the shelf is read from
 * somewhere in the plan. plan.test.ts holds it to those rules.
 *
 * Three stages, ten blocks, three years. The stages are the reader's
 * question: what does a beginner, a journeyman and an expert each need to
 * be able to do, and what is the reading for each. The blocks are the
 * answer, one gate at a time.
 */

import {
  type Block,
  type CadenceItem,
  tasksOf,
} from "../../progress/core/curriculum";

export const documentKey = "signal-processing";

export interface Idea {
  name: string;
  /** The idea, in two sentences. */
  idea: string;
  /** What owning it looks like: something you can do, not something you know. */
  test: string;
}

export interface Stage {
  id: "beginner" | "journeyman" | "expert";
  title: string;
  /** When in the plan the stage runs. */
  weeks: string;
  /** What the stage is for. */
  why: string;
  /** What being done with it looks like. */
  exit: string;
  blocks: Block[];
}

export type Level = Stage["id"];

export interface Book {
  title: string;
  authors: string;
  edition?: string;
  level: Level;
  url: string;
  free: boolean;
  /** Why this one and not another on the same subject. */
  why: string;
}

export const levels: Record<Level, string> = {
  beginner: "Beginner",
  journeyman: "Journeyman",
  expert: "Expert",
};

export const copy = {
  intro:
    "A three-year plan for signal processing, from the first Fourier transform to reproducing papers. I wrote it for myself and I am working through it alongside the field radio plan and the electrical engineering master's: the radio supplies the signals, the degree supplies the proofs, and this is the practice in between.",
  note: "The checklist and the gate log are mine. Signed out, the page is read-only.",
  scoring:
    "Blocks are scored on gates, not hours. A gate is a thing built or a problem set finished, with a tolerance or a clock on it, and the block is done when the gate passes.",
  practice:
    "Each block lists practice that scores you as well as reading: problem sets with answers, graded courses, plots that are right or wrong. Those cannot report back here, so I write their results in the gate log: a problem set score, a time, a measured error in decibels.",
  ideas:
    "Eight ideas carry the whole subject; everything after is one of them applied. Each stage returns to all eight at more depth, and the second column is what owning one looks like.",
  bookshelf:
    "Every book the blocks read from, in one place, with what it is for. Free ones are marked. The paid ones are worth a used copy of the edition named, and a library will have most of them.",
};

export const rules: string[] = [
  "Look at it in both domains. Before and after every operation, plot the signal in time and in frequency. Most mistakes are visible in a plot nobody made.",
  "Never read past an exercise. A chapter whose problems are not done is a chapter not read.",
  "Build it before you trust it. Every transform, filter and estimator in the plan is written once by hand, from the definition, before a library version is allowed.",
  "Real signals, not synthetic ones, wherever the block allows. The radio, a microphone and a sensor are the sources; the noise, drift and clipping they bring are the lesson.",
  "Write the derivation down. A result you cannot rederive on paper in ten minutes is a result you are borrowing.",
  "Keep the log. A block is done when its gate passes, and the gate is scored from the log, not from how it felt.",
];

export const cadence: CadenceItem[] = [
  {
    label: "Study",
    detail:
      "Five hours a week from the block's reading, in sittings of at least an hour, with the problems done as they come rather than saved for later.",
  },
  {
    label: "Code",
    detail:
      "Three hours a week in Python with NumPy and SciPy, or MATLAB. Every idea from the week's reading is run against a signal the same week.",
  },
  {
    label: "Derive",
    detail:
      "Twenty minutes a day rederiving one result from memory on paper: a transform pair, a filter's response, an estimator's variance. Timed, checked against the book, kept.",
  },
  {
    label: "Real signals",
    detail:
      "One recording a week from the radio, a microphone or a sensor, processed end to end and plotted in both domains.",
  },
  {
    label: "Review",
    detail:
      "Sunday evening: read the week's log, score the derivations, write the next week's three priorities.",
  },
];

export const ideas: Idea[] = [
  {
    name: "Two views of every signal",
    idea: "Time and frequency are the same information in different coordinates, and the Fourier transform is the change of basis. Every operation has a meaning in both.",
    test: "You say what a time-domain operation does to the spectrum, and the reverse, before running it, and the plot agrees.",
  },
  {
    name: "Linear time-invariant systems",
    idea: "A linear time-invariant system is its impulse response. Convolution in time is multiplication in frequency, which is filtering, echo, blur and a radio channel in one sentence.",
    test: "Given an impulse response you sketch the frequency response by hand; given a spectrum you say what the system does to a step.",
  },
  {
    name: "Sampling and aliasing",
    idea: "Sampling below twice the bandwidth folds high frequencies onto low ones, and no later processing can undo it. This is why anti-alias filters exist and why sampling faster is rarely the first fix.",
    test: "You choose a sample rate and an anti-alias filter for a new sensor from its bandwidth, and you can show an alias on a plot and name where it came from.",
  },
  {
    name: "The time-frequency tradeoff",
    idea: "Resolution in time and in frequency trade off; a short window locates an event and smears its frequency. Spectrograms, wavelets and filter banks are different bargains with the same limit.",
    test: "You pick a window and its length for a given signal, say what the choice costs, and read leakage off a spectrum without being told it is there.",
  },
  {
    name: "Poles and zeros",
    idea: "A discrete filter is a rational function of z. Zeros carve notches, poles make resonances, the pole radius decides stability, and the phase response is a design choice, not an accident.",
    test: "You place poles and zeros by hand to meet a rough specification, and say whether the result is stable and what its group delay does to a pulse.",
  },
  {
    name: "Noise is a signal too",
    idea: "Real work is estimating something from a noisy measurement. Signal-to-noise ratio, power spectral density, correlation, the matched filter and the Cramér-Rao bound are the vocabulary.",
    test: "You say how much averaging a measurement needs for a target signal-to-noise ratio, and why the matched filter is the best you can do against white noise.",
  },
  {
    name: "Finite precision",
    idea: "Quantization adds noise, coefficient rounding moves poles, and fixed-point arithmetic overflows and wraps. Textbook filters fail in hardware for these reasons and no others.",
    test: "You take a floating-point filter to fixed point, predict its noise floor before running it, and show on a pole-zero plot where the rounding moved the poles.",
  },
  {
    name: "It is all linear algebra",
    idea: "Every transform is a change of basis. Fourier, cosine and wavelet bases are chosen; principal components are learned from the data. Seeing transforms as projections is what lets you invent one.",
    test: "You write the discrete Fourier transform as a matrix, say when it is orthogonal, and derive a new transform from a property you want it to have.",
  },
];

const beginner: Block[] = [
  {
    id: "maths",
    title: "The mathematics the field assumes",
    weeks: "Months 1 to 2",
    why: "Signal processing is complex numbers, linear algebra and a little probability worn smooth. The block is short because the point is fluency, not coverage; the proofs come in the journeyman stage.",
    tasks: [
      {
        id: "maths-complex",
        text: "Complex numbers until they are boring: rectangular and polar forms, Euler's formula both ways, multiplication as rotation, roots of unity. Smith's chapter 30 and 3Blue1Brown's Euler video.",
      },
      {
        id: "maths-linear",
        text: "Watch the Essence of Linear Algebra series, then work MIT 18.06 through eigenvalues and orthogonal projections, with the problem sets. Vectors as signals and matrices as systems is the reading you are doing it for.",
      },
      {
        id: "maths-probability",
        text: "Stat 110 through expectation, variance and the normal distribution, with its strategic practice problems. Independence and the variance of a sum are what every later noise calculation rests on.",
      },
      {
        id: "maths-tools",
        text: "Set up Python with NumPy, SciPy, Matplotlib and Jupyter. Generate a sine, a square wave, white noise and a chirp; plot each; save the notebook. Everything after runs in it.",
      },
      {
        id: "maths-anki",
        text: "Start an Anki deck for the formulas, and add to it in every block from here on: transform pairs, filter relations, bounds.",
      },
    ],
    gate: "Twenty problems in ten minutes with no errors: complex multiplication and division in both forms, Euler's formula in both directions, a two by two inverse and its eigenvalues, and the mean and variance of a sum of independent variables.",
    resources: [
      {
        title:
          "The Scientist and Engineer's Guide to DSP, chapter 30: Complex numbers",
        url: "https://www.dspguide.com/ch30.htm",
        note: "Free. The only chapter of this book to read out of order.",
      },
      {
        title: "Essence of Linear Algebra",
        url: "https://www.3blue1brown.com/topics/linear-algebra",
        note: "Free. Sixteen short videos; the pictures the rest of the plan assumes you carry.",
      },
      {
        title: "MIT 18.06 Linear Algebra",
        url: "https://ocw.mit.edu/courses/18-06-linear-algebra-spring-2010/",
        note: "Free. Strang's lectures, problem sets and exams.",
      },
      {
        title: "Introduction to Applied Linear Algebra",
        url: "https://web.stanford.edu/~boyd/vmls/",
        note: "Free. Boyd and Vandenberghe. Vectors, matrices and least squares with the signals already in the examples; the book to keep open through the whole plan.",
      },
      {
        title: "Stat 110: Probability",
        url: "https://projects.iq.harvard.edu/stat110/home",
        note: "Free. Blitzstein's lectures and the book, which is also free online.",
      },
    ],
    practice: [
      {
        title: "MIT 18.06 problem sets and exams",
        url: "https://ocw.mit.edu/courses/18-06-linear-algebra-spring-2010/pages/assignments/",
        note: "Solutions are posted. Do the problem set before the solution, score it, log the fraction right.",
      },
      {
        title: "Stat 110 strategic practice",
        url: "https://projects.iq.harvard.edu/stat110/strategic-practice-problems",
        note: "Problems grouped by theme with solutions. Log each set as a fraction right.",
      },
      {
        title: "Anki",
        url: "https://apps.ankiweb.net/",
        note: "Spaced repetition for the formulas. The daily review count and the retention rate are the score.",
      },
    ],
  },
  {
    id: "domains",
    title: "Signals in two domains",
    weeks: "Months 2 to 4",
    why: "The Fourier transform is the field's one trick, and it is learned by computing it, plotting it and being surprised. Smith's book is the text because every chapter ends in something you can run.",
    tasks: [
      {
        id: "domains-smith",
        text: "Read Smith chapters 1 to 13: statistics and noise, the ADC, linear systems, convolution and its properties, the discrete Fourier transform and its applications. Do every example in code.",
      },
      {
        id: "domains-dft",
        text: "Write the discrete Fourier transform from its definition, as a double loop and then as a matrix. Check it against the FFT to one part in a million. Then time both at a thousand points and at a million.",
      },
      {
        id: "domains-convolution",
        text: "Write convolution by hand, then prove to yourself in code that it is multiplication in frequency: convolve two signals both ways and plot the difference.",
      },
      {
        id: "domains-spectrogram",
        text: "Build a spectrogram from scratch: frame, window, transform, stack. Try a rectangular, a Hann and a Blackman window on the same recording and keep the three plots side by side.",
      },
      {
        id: "domains-alias",
        text: "Alias a chirp on purpose. Sample it too slowly, plot what comes back, and explain on the plot where each folded frequency came from. Then put an anti-alias filter in front and do it again.",
      },
      {
        id: "domains-real",
        text: "Record a minute from a microphone and a minute of IQ from the radio. Find the tones, the hum, the noise floor and the carrier in each, in both domains, and label them.",
      },
      {
        id: "domains-thinkdsp",
        text: "Work Think DSP chapters 1 to 7 in its notebooks. It is the same material a second way, in code first, and the exercises are the point.",
      },
    ],
    gate: "From a bare Python session: write the DFT from its definition, check it against the FFT to within one part in a million, then build a spectrogram of a recording you made and point to the leakage, the aliasing you introduced deliberately, and the window's main lobe, all inside sixty minutes.",
    resources: [
      {
        title:
          "The Scientist and Engineer's Guide to Digital Signal Processing",
        url: "https://www.dspguide.com/",
        note: "Free. Steven W. Smith. The beginner's text: no proofs, every idea shown, every chapter runnable.",
      },
      {
        title: "Think DSP",
        url: "https://greenteapress.com/wp/think-dsp/",
        note: "Free. Allen Downey. Signal processing in Python, code before mathematics, with notebooks for every chapter.",
      },
      {
        title: "Seeing Circles, Sines, and Signals",
        url: "https://jackschaedler.github.io/circles-sines-signals/",
        note: "Free. Jack Schaedler. An interactive primer on the DFT; an evening, before Smith's chapter 8.",
      },
      {
        title: "But what is the Fourier transform? A visual introduction",
        url: "https://www.3blue1brown.com/lessons/fourier-transforms/",
        note: "3Blue1Brown. Twenty minutes; watch it before Smith's chapter 8 and again after.",
      },
      {
        title:
          "The Fast Fourier Transform (FFT): most ingenious algorithm ever?",
        url: "https://www.youtube.com/watch?v=h7apO7q16V0",
        note: "Reducible. Why the FFT is fast, from polynomial multiplication up.",
      },
      {
        title: "SciPy signal processing tutorial",
        url: "https://docs.scipy.org/doc/scipy/tutorial/signal.html",
        note: "The library you will lean on once you have written each thing once yourself.",
      },
    ],
    practice: [
      {
        title: "Digital Signal Processing 1: Basic Concepts and Algorithms",
        url: "https://www.coursera.org/learn/dsp1",
        note: "Prandoni and Vetterli, EPFL; the first of four courses. Graded quizzes and programming assignments, free to audit. Log every quiz score.",
      },
      {
        title: "Think DSP exercises",
        url: "https://github.com/AllenDowney/ThinkDSP",
        note: "Every chapter ends in exercises with solution notebooks. Do them before looking; log which you got without the solution.",
      },
      {
        title: "Table of Fourier transform pairs",
        url: "https://en.wikipedia.org/wiki/Fourier_transform#Tables_of_important_Fourier_transforms",
        note: "Ten pairs from memory in ten minutes, checked against the table. Log the count right; twenty is the standard by the end of the block.",
      },
    ],
  },
  {
    id: "filters",
    title: "Filters",
    weeks: "Months 4 to 6",
    why: "A filter is the first thing anyone is paid to build, and the first place the two domains have to be held in mind at once. Finite and infinite impulse response, windows, poles and zeros, and the phase you did not think about.",
    tasks: [
      {
        id: "filters-smith",
        text: "Read Smith chapters 14 to 21: filter basics, moving average, windowed sinc, custom filters, FFT convolution, recursive filters, Chebyshev filters, and comparing the two families.",
      },
      {
        id: "filters-fir",
        text: "Design a windowed-sinc lowpass by hand: sinc, window, truncate, normalise. Compare its response to SciPy's firwin with the same specification and explain every difference.",
      },
      {
        id: "filters-notch",
        text: "Remove mains hum from a real recording with a second-order notch you designed from its pole-zero plot. Show the before and after spectra and the hum's harmonics you missed.",
      },
      {
        id: "filters-phase",
        text: "Pass a square wave through a linear-phase FIR and a Butterworth IIR with the same magnitude response. Plot both outputs and the group delay of each; the ringing is the lesson.",
      },
      {
        id: "filters-biquad",
        text: "Implement a biquad in direct form II transposed and run it on a live audio stream in real time. Add a control that moves the pole radius and listen to what stability sounds like.",
      },
      {
        id: "filters-resample",
        text: "Resample a recording between 44.1 and 48 kilohertz with your own polyphase interpolator, then back, and measure the error against the original in decibels.",
      },
      {
        id: "filters-jos",
        text: "Read Julius Smith's Introduction to Digital Filters through the chapters on transfer functions, poles and zeros, and frequency response. It is the same ground as Smith with the mathematics put back.",
      },
    ],
    gate: "Given a specification with a passband edge, a stopband edge, a ripple and an attenuation: design one FIR and one IIR filter that meet it, implement both without a filter design library, and show the measured response meets every number within half a decibel, inside two hours.",
    resources: [
      {
        title:
          "The Scientist and Engineer's Guide to DSP, chapter 14: Introduction to digital filters",
        url: "https://www.dspguide.com/ch14.htm",
        note: "Free. Chapters 14 to 21 are the block.",
      },
      {
        title: "Introduction to Digital Filters with Audio Applications",
        url: "https://ccrma.stanford.edu/~jos/filters/",
        note: "Free. Julius O. Smith, Stanford. The clearest treatment of poles, zeros and the z-transform that exists, with audio to listen to.",
      },
      {
        title: "Understanding Digital Signal Processing",
        url: "https://www.pearson.com/en-us/subject-catalog/p/Lyons-Understanding-Digital-Signal-Processing-3rd-Edition/P200000000443",
        note: "Richard G. Lyons, third edition. The practitioner's book: what the equations mean and the tricks nobody writes down. Read alongside Smith, and keep it for the journeyman stage.",
      },
    ],
    practice: [
      {
        title: "Digital Signal Processing 2: Filtering",
        url: "https://www.coursera.org/learn/dsp2",
        note: "The second EPFL course, on filters. Graded; log every quiz and assignment score.",
      },
      {
        title: "fiiir: FIR filter design tool",
        url: "https://fiiir.com/",
        note: "Design a filter there, then reproduce its coefficients yourself from the same specification. Log the largest coefficient difference and the response difference in decibels.",
      },
      {
        title: "Filter design drill",
        url: "https://docs.scipy.org/doc/scipy/reference/signal.html#filter-design",
        note: "Pick a random specification, design by hand, check against the library. Five in an hour, each within one decibel of specification, is the standard.",
      },
    ],
  },
];

const journeyman: Block[] = [
  {
    id: "systems",
    title: "Signals and systems, rigorously",
    weeks: "Months 7 to 10",
    why: "The beginner stage was pictures and code. This is the same material with proofs, done through the problem sets of the course everyone else took. It is where the intuition gets checked against the mathematics and found to be mostly right.",
    tasks: [
      {
        id: "systems-oppenheim",
        text: "Oppenheim and Willsky chapters 1 to 7 and 9 to 10: signals, linear time-invariant systems, Fourier series, the continuous and discrete-time Fourier transforms, time and frequency characterisation, sampling, the Laplace and z-transforms. Every basic problem, half the advanced ones.",
      },
      {
        id: "systems-6003",
        text: "Follow MIT 6.003 on OpenCourseWare alongside the book: the lectures, the recitations and every problem set, done before the posted solution.",
      },
      {
        id: "systems-sampling",
        text: "Prove the sampling theorem on paper from the Fourier transform of an impulse train, then write the reconstruction and show where it fails for a signal that is not bandlimited.",
      },
      {
        id: "systems-laplace",
        text: "For one analog circuit, a second-order RLC: derive the transfer function, the poles, the step response and the frequency response, then measure them on the bench or in a simulator.",
      },
      {
        id: "systems-schaum",
        text: "Work Schaum's Outline as the drill book: fifty solved problems a week across the chapters already covered, timed, and the mistakes logged by kind.",
      },
    ],
    gate: "MIT 6.003's two quizzes and the final, taken closed book under their own time limits from the archive, each scored at eighty percent or better.",
    resources: [
      {
        title: "Signals and Systems",
        url: "https://www.pearson.com/en-us/subject-catalog/p/signals-and-systems/P200000003155/9780138147570",
        note: "Oppenheim and Willsky, second edition. The standard text for a reason: the problems.",
      },
      {
        title: "Signals and Systems: Theory and Applications",
        url: "https://ss2-2e.eecs.umich.edu/",
        note: "Free. Ulaby and Yagle, second edition, from Michigan. The same ground as Oppenheim and Willsky, with solutions on the site, if the paid book is out of reach.",
      },
      {
        title: "MIT 6.003 Signals and Systems",
        url: "https://ocw.mit.edu/courses/6-003-signals-and-systems-fall-2011/",
        note: "Free. Lectures, recitations, problem sets, exams, all with solutions.",
      },
      {
        title: "Schaum's Outline of Signals and Systems",
        url: "https://www.mheducation.com/highered/mhp/product/schaum-s-outline-signals-systems-fourth-edition.html",
        note: "Hwei Hsu, fourth edition. Hundreds of solved problems; the drill book for this block and the next.",
      },
      {
        title: "Barry Van Veen: All Signal Processing",
        url: "https://www.youtube.com/@allsignalprocessing",
        note: "Free. Short lectures on every topic in the block, with a map of what each one assumes. For the one idea a chapter that did not land.",
      },
      {
        title: "Stanford EE261: The Fourier Transform and its Applications",
        url: "https://see.stanford.edu/Course/EE261",
        note: "Free. Brad Osgood's lectures. The Fourier transform in more depth than either textbook, including distributions; watch after the sampling chapter.",
      },
    ],
    practice: [
      {
        title: "MIT 6.003 problem sets",
        url: "https://ocw.mit.edu/courses/6-003-signals-and-systems-fall-2011/pages/assignments/",
        note: "Eleven sets with solutions. Score each before reading the solution; log the fraction right.",
      },
      {
        title: "MIT 6.003 exams",
        url: "https://ocw.mit.edu/courses/6-003-signals-and-systems-fall-2011/pages/exams/",
        note: "Two quizzes and a final, with solutions. These are the gate; take them under the clock and log the percentage.",
      },
    ],
  },
  {
    id: "dtsp",
    title: "Discrete-time signal processing",
    weeks: "Months 10 to 14",
    why: "The professional core of the subject, from the book every practitioner has on the shelf and the lectures its author gave. Multirate, filter design done properly, spectral analysis, and what finite precision does to all of it.",
    tasks: [
      {
        id: "dtsp-oppenheim",
        text: "Oppenheim and Schafer chapters 2 to 10: discrete-time signals and systems, the z-transform, sampling, transform analysis of systems, structures, filter design, the DFT, the FFT, and Fourier analysis with the DFT. The problems are long; do them anyway.",
      },
      {
        id: "dtsp-lectures",
        text: "Watch Oppenheim's own lecture series alongside the book, one lecture per chapter section, and the MIT 6.341 problem sets with them.",
      },
      {
        id: "dtsp-fft",
        text: "Write a radix-2 decimation-in-time FFT from the butterfly up, then a mixed-radix one. Match NumPy to one part in a billion and profile where the time goes.",
      },
      {
        id: "dtsp-parks",
        text: "Implement the Parks-McClellan algorithm from the Remez exchange, not from a library, and reproduce the equiripple filter in the book's example to the last coefficient.",
      },
      {
        id: "dtsp-multirate",
        text: "Build a rational sample-rate converter from polyphase decimation and interpolation, then a cascade of half-band stages, and show the aliasing budget of each in a plot.",
      },
      {
        id: "dtsp-fixed",
        text: "Take a fourth-order IIR to sixteen-bit fixed point on a microcontroller with CMSIS-DSP: choose the structure, scale each section, predict the noise floor and the pole movement on paper, then measure both.",
      },
      {
        id: "dtsp-spectral",
        text: "Estimate a power spectrum three ways, periodogram, Bartlett and Welch, on the same noise recording, and show the bias and variance of each against what the book predicts.",
      },
      {
        id: "dtsp-lyons",
        text: "Read Lyons in full now. Every chapter you found obvious is one Oppenheim taught you; every one you did not is a gap to log.",
      },
    ],
    gate: "A fixed-point IIR filter running on a microcontroller in real time, with the measured noise floor and passband within one decibel of what you predicted on paper before flashing it, and the prediction in the log before the measurement.",
    resources: [
      {
        title: "Discrete-Time Signal Processing",
        url: "https://ocw.mit.edu/courses/res-6-dtsp-discrete-time-signal-processing/",
        note: "Free. Oppenheim and Schafer, third edition, the whole book as a PDF on MIT OpenCourseWare since 2026. Everything in the block is in it.",
      },
      {
        title: "MIT RES.6-008 Digital Signal Processing",
        url: "https://ocw.mit.edu/courses/res-6-008-digital-signal-processing-spring-2011/",
        note: "Free. Oppenheim's twenty lectures, filmed. Older than the book and the same author, so they agree.",
      },
      {
        title: "MIT 6.341 Discrete-Time Signal Processing",
        url: "https://ocw.mit.edu/courses/6-341-discrete-time-signal-processing-fall-2005/",
        note: "Free. The graduate course on the book, with problem sets and solutions.",
      },
      {
        title: "Mathematics of the Discrete Fourier Transform",
        url: "https://ccrma.stanford.edu/~jos/mdft/",
        note: "Free. Julius O. Smith. The DFT from complex numbers up, with every proof.",
      },
      {
        title: "Rich Radke: Digital Signal Processing",
        url: "https://sites.ecse.rpi.edu/~rjradke/dspcourse.html",
        note: "Free. Twenty-five full lectures from Rensselaer, following Proakis and Manolakis. The second angle on every chapter, filmed in a real classroom.",
      },
      {
        title:
          "Digital Signal Processing: Principles, Algorithms, and Applications",
        url: "https://www.pearson.com/en-us/subject-catalog/p/digital-signal-processing-principles-algorithms-and-applications/P200000003415/9780137348657",
        note: "Proakis and Manolakis, fifth edition. The other standard text, with more problems than Oppenheim and a chapter on filter banks and wavelets the older book lacks.",
      },
      {
        title: "CMSIS-DSP",
        url: "https://arm-software.github.io/CMSIS-DSP/latest/",
        note: "Arm's fixed and floating-point library for Cortex-M. The fixed-point task runs on it.",
      },
    ],
    practice: [
      {
        title: "MIT 6.341 problem sets",
        url: "https://ocw.mit.edu/courses/6-341-discrete-time-signal-processing-fall-2005/pages/assignments/",
        note: "With solutions. Score each before reading the solution; log the fraction right and the time taken.",
      },
      {
        title: "Cambridge Digital Signal Processing exercises",
        url: "https://www.cl.cam.ac.uk/teaching/2526/DSP/",
        note: "Markus Kuhn's course: compact notes and short programming exercises with a radio and radar flavour. Do the exercise sheets; log the fraction right.",
      },
      {
        title: "Digital signal processing exercises, Rostock",
        url: "https://github.com/spatialaudio/digital-signal-processing-exercises",
        note: "Notebooks on the DFT, filter design, spectral estimation and quantisation, runnable in the browser. Each ends in a result to check; log which matched.",
      },
      {
        title: "Digital Signal Processing 3: Analog vs Digital",
        url: "https://www.coursera.org/learn/dsp3",
        note: "The third EPFL course: sampling, interpolation, multirate and quantization. Graded; log the scores.",
      },
      {
        title: "Digital Signal Processing 4: Applications",
        url: "https://www.coursera.org/learn/dsp4",
        note: "The fourth EPFL course: a modem, image processing, and real-time. Graded; the modem is the bridge to the radio block.",
      },
    ],
  },
  {
    id: "noise",
    title: "Noise, estimation and detection",
    weeks: "Months 14 to 18",
    why: "Everything so far assumed you knew the signal. Real work is pulling one out of noise, and saying how well that can be done at all. Statistical signal processing is the half of the subject the deterministic books leave out, and it is where most industrial work lives.",
    tasks: [
      {
        id: "noise-random",
        text: "Random processes properly: stationarity, autocorrelation, power spectral density and the Wiener-Khinchin theorem, and what a linear system does to each. Kay's probability book, then MIT 6.011's notes.",
      },
      {
        id: "noise-matched",
        text: "Derive the matched filter, implement it, and measure its signal-to-noise gain on a pulse in white noise against the theory. Then colour the noise and show it stop being optimal.",
      },
      {
        id: "noise-crlb",
        text: "Derive the Cramér-Rao bound for the frequency of a sinusoid in white noise. Implement the maximum likelihood estimator and plot its variance against the bound across signal-to-noise ratio; find the threshold.",
      },
      {
        id: "noise-wiener",
        text: "Build a Wiener filter for a noisy recording from measured spectra, and compare it to the best fixed filter you can design by hand for the same recording.",
      },
      {
        id: "noise-kalman",
        text: "Work through Kalman and Bayesian Filters in Python, then write a Kalman filter for a tone whose frequency drifts, and for a position track from noisy range measurements, and tune each from the innovation sequence.",
      },
      {
        id: "noise-detection",
        text: "Neyman-Pearson detection: an energy detector and a matched detector for a known signal in noise, with measured receiver operating characteristic curves against the theoretical ones.",
      },
      {
        id: "noise-kay",
        text: "Kay's Estimation Theory chapters 1 to 8 and 10 to 13, and Detection Theory chapters 1 to 7. Both books' problems are the reading.",
      },
    ],
    gate: "A frequency estimator for a tone in noise whose measured variance sits within one decibel of the Cramér-Rao bound above the threshold, with the bound derived by hand in the log and the threshold effect shown on the plot.",
    resources: [
      {
        title:
          "Fundamentals of Statistical Signal Processing, Volume I: Estimation Theory",
        url: "https://www.amazon.com/Fundamentals-Statistical-Signal-Processing-Estimation/dp/0133457117",
        note: "Steven M. Kay. The estimation book; the Cramér-Rao bound, maximum likelihood and least squares done once and properly.",
      },
      {
        title:
          "Fundamentals of Statistical Signal Processing, Volume II: Detection Theory",
        url: "https://www.amazon.com/Fundamentals-Statistical-Signal-Processing-Detection/dp/013504135X",
        note: "Steven M. Kay. The detection book, same author, same care.",
      },
      {
        title: "Intuitive Probability and Random Processes using MATLAB",
        url: "https://link.springer.com/book/10.1007/b104645",
        note: "Steven M. Kay. The probability the two volumes assume, taught by the same hand, with code.",
      },
      {
        title: "MIT 6.011 Signals, Systems and Inference",
        url: "https://ocw.mit.edu/courses/6-011-signals-systems-and-inference-spring-2018/",
        note: "Free. Oppenheim and Verghese's course, with the full text of their book as lecture notes. The bridge from deterministic to statistical.",
      },
      {
        title: "Kalman and Bayesian Filters in Python",
        url: "https://rlabbe.github.io/Kalman-and-Bayesian-Filters-in-Python/",
        note: "Free. Roger Labbe. The Kalman filter taught by building it in notebooks, from the one-dimensional case up to the unscented and particle filters.",
      },
    ],
    practice: [
      {
        title: "MIT 6.011 problem sets",
        url: "https://ocw.mit.edu/courses/6-011-signals-systems-and-inference-spring-2018/pages/assignments/",
        note: "With solutions. Score each before the solution; log the fraction right.",
      },
      {
        title: "Kalman and Bayesian Filters in Python, exercises",
        url: "https://rlabbe.github.io/Kalman-and-Bayesian-Filters-in-Python/",
        note: "Each chapter's exercises have solutions in the notebook. Do them blind; log which needed the solution.",
      },
      {
        title: "Cramér-Rao bound drill",
        url: "https://en.wikipedia.org/wiki/Cram%C3%A9r%E2%80%93Rao_bound",
        note: "Derive the bound for a new parameter from memory, one a week: amplitude, phase, delay, frequency, direction. Timed; log the minutes and whether it matched the book.",
      },
    ],
  },
  {
    id: "radio",
    title: "A domain: radio",
    weeks: "Months 12 to 18, alongside the two blocks above",
    why: "Depth in one application is what makes the theory stick, and radio is the one whose signals are already in the pack. Every idea in the plan appears in a receiver, and a receiver you wrote every line of is the proof you understood them.",
    tasks: [
      {
        id: "radio-pysdr",
        text: "Work PySDR end to end with the receiver from the radio plan: IQ sampling, the frequency domain, filtering, digital modulation, pulse shaping, synchronisation, channel coding. Every exercise, against live signals.",
      },
      {
        id: "radio-fm",
        text: "Write a broadcast FM demodulator in NumPy from raw IQ: channel filter, decimate, discriminate, de-emphasise, resample to audio. No signal processing library beyond the FFT.",
      },
      {
        id: "radio-digital",
        text: "Write a phase-shift-keyed demodulator with your own matched filter, timing recovery and carrier recovery, and decode a real over-the-air digital signal end to end.",
      },
      {
        id: "radio-ber",
        text: "In loopback with additive white noise, measure bit error rate against energy per bit over noise density for your demodulator and plot it on the theoretical curve. Every decibel of gap is a bug or a lesson.",
      },
      {
        id: "radio-ofdm",
        text: "Build an orthogonal frequency-division multiplexing transmitter and receiver in loopback: cyclic prefix, pilots, channel estimation, equalisation. Then put a multipath channel between them and watch the equaliser earn its keep.",
      },
      {
        id: "radio-gnuradio",
        text: "Rebuild the FM and the digital receivers in GNU Radio from stock blocks, compare their output to yours sample for sample, and write one custom block in Python.",
      },
      {
        id: "radio-rice",
        text: "Read Rice's Digital Communications: A Discrete-Time Approach for the synchronisation chapters. It is the book that treats a receiver as a signal processing problem rather than a communications one.",
      },
    ],
    gate: "A demodulator you wrote, no GNU Radio blocks, that decodes a real over-the-air digital signal end to end, with a measured bit error rate in loopback within one decibel of the theoretical curve across the range you tested.",
    resources: [
      {
        title: "PySDR: A Guide to SDR and DSP using Python",
        url: "https://pysdr.org/",
        note: "Free. Marc Lichtman. The best applied signal processing text on the web, every idea with Python you run against the receiver.",
      },
      {
        title: "Signal Processing for Communications",
        url: "https://www.sp4comm.org/",
        note: "Free. Prandoni and Vetterli. The EPFL courses' textbook, ending in a modem built from the theory.",
      },
      {
        title: "Digital Communications: A Discrete-Time Approach",
        url: "https://www.pearson.com/en-us/subject-catalog/p/digital-communications-a-discrete-time-approach/P200000003211",
        note: "Michael Rice. Synchronisation, timing and carrier recovery as discrete-time signal processing. Nothing else covers it as well.",
      },
      {
        title: "GNU Radio tutorials",
        url: "https://wiki.gnuradio.org/index.php/Tutorials",
        note: "Free. The official tutorials, which are the course; the wiki is the reference.",
      },
    ],
    practice: [
      {
        title: "PySDR exercises",
        url: "https://pysdr.org/",
        note: "Every chapter ends in exercises against the receiver. Log which decode and which do not, and what the fix was.",
      },
      {
        title: "GNU Radio tutorials",
        url: "https://wiki.gnuradio.org/index.php/Tutorials",
        note: "Each tutorial ends in a flowgraph that works or does not. Log the time from opening the tutorial to a working graph.",
      },
      {
        title: "Bit error rate against Eb/N0",
        url: "https://en.wikipedia.org/wiki/Eb/N0",
        note: "The theoretical curves for each modulation. Your loopback measurement against them is the score; log the gap in decibels at three points.",
      },
    ],
  },
];

const expert: Block[] = [
  {
    id: "bases",
    title: "Foundations, bases and sparsity",
    weeks: "Months 19 to 24",
    why: "The journeyman knew the transforms. The expert knows why they are the ones they are and how to make a new one: Hilbert spaces, frames, wavelets, and the discovery that a signal sparse in some basis can be recovered from far fewer samples than Nyquist asks for.",
    tasks: [
      {
        id: "bases-fsp",
        text: "Foundations of Signal Processing chapters 2 to 6: Hilbert spaces, sequences and discrete-time systems, functions and continuous-time systems, sampling and interpolation, approximation and estimation. The exercises are the reading.",
      },
      {
        id: "bases-wavelets",
        text: "Implement the discrete wavelet transform for Haar and Daubechies-4 from the filter bank, verify perfect reconstruction to machine precision, and denoise a recording by thresholding coefficients. Compare it to the Wiener filter from the noise block on the same recording.",
      },
      {
        id: "bases-filterbank",
        text: "Build a two-channel filter bank, prove the perfect reconstruction conditions on paper, then break them one at a time and show each failure on a plot.",
      },
      {
        id: "bases-sparse",
        text: "Implement orthogonal matching pursuit and basis pursuit, recover an undersampled sparse signal with each, and reproduce the phase transition between success and failure as a function of measurements and sparsity.",
      },
      {
        id: "bases-frames",
        text: "Construct a tight frame that is not a basis, show its redundancy buys robustness to erasures, and derive the frame bounds by hand.",
      },
      {
        id: "bases-mallat",
        text: "Mallat chapters 1 to 4 and 7, and the compressed sensing chapter, as the second pass with more depth.",
      },
    ],
    gate: "A sparse recovery experiment reproducing the published phase transition to within five percent of the curve, with your own matching pursuit and your own wavelet transform, and a two-page write-up someone at the journeyman stage can follow.",
    resources: [
      {
        title: "Foundations of Signal Processing",
        url: "https://www.fourierandwavelets.org/",
        note: "Free. Vetterli, Kovačević and Goyal. The subject rebuilt from linear algebra and approximation, with the companion volume on Fourier and wavelets.",
      },
      {
        title: "A Wavelet Tour of Signal Processing: The Sparse Way",
        url: "https://wavelet-tour.github.io/",
        note: "Stéphane Mallat, third edition. The wavelet book, now also the sparsity book.",
      },
      {
        title: "An Introduction to Compressive Sampling",
        url: "https://authors.library.caltech.edu/records/epx8s-y1b11",
        note: "Free, the authors' copy. Candès and Wakin, IEEE Signal Processing Magazine, 2008. The survey that started most people; ten pages.",
      },
      {
        title: "A Mathematical Introduction to Compressive Sensing",
        url: "https://link.springer.com/book/10.1007/978-0-8176-4948-7",
        note: "Foucart and Rauhut. The theory behind the survey, with proofs; the first textbook on the subject and still the reference.",
      },
      {
        title: "Linear Algebra and Learning from Data",
        url: "https://math.mit.edu/~gs/learningfromdata/",
        note: "Gilbert Strang. The linear algebra the block leans on, from the person who taught it in the first block, now with the data-driven bases.",
      },
    ],
    practice: [
      {
        title: "Foundations of Signal Processing exercises",
        url: "https://www.fourierandwavelets.org/",
        note: "Solutions to selected exercises are on the site. Do a chapter's exercises before checking; log the fraction right.",
      },
      {
        title: "An Introduction to Compressive Sampling, figures",
        url: "https://authors.library.caltech.edu/records/epx8s-y1b11",
        note: "Reproduce every figure in the paper from scratch. Log which you could reproduce and the largest discrepancy.",
      },
    ],
  },
  {
    id: "adaptive",
    title: "Adaptive, statistical and array processing",
    weeks: "Months 24 to 30",
    why: "The filters so far were designed once and fixed. The world is not: channels change, noise moves, and sources are somewhere. Adaptive filters, nonlinear state estimation and arrays are the three ways the subject follows a moving target.",
    tasks: [
      {
        id: "adaptive-lms",
        text: "Least mean squares, normalised least mean squares and recursive least squares, from the derivation to code, and their convergence measured against the theory on a channel you defined.",
      },
      {
        id: "adaptive-canceller",
        text: "An adaptive noise canceller and an acoustic echo canceller on real audio from two microphones, with the misadjustment and convergence time logged against Haykin's predictions.",
      },
      {
        id: "adaptive-nonlinear",
        text: "The extended, unscented and particle filters on one nonlinear tracking problem, bearings-only from a moving observer, with the error of each against the posterior Cramér-Rao bound.",
      },
      {
        id: "adaptive-doa",
        text: "Direction of arrival: a delay-and-sum beamformer, then MUSIC and ESPRIT, on synthetic data first and then on a real array, four coherent receivers or a microphone array you built.",
      },
      {
        id: "adaptive-spectral",
        text: "Parametric spectral estimation: autoregressive models, Capon and MUSIC against the periodogram on the same data, with each method's resolution limit shown against Stoica and Moses.",
      },
      {
        id: "adaptive-haykin",
        text: "Haykin chapters 1 to 10 and 14 to 15, Simon chapters 1 to 7 and 13 to 15, Van Trees chapters 2 to 4 and 8 to 9. Long books; the computer experiments in each are the part that counts.",
      },
    ],
    gate: "Direction of arrival on a real array you built, MUSIC against a beamformer, with the measured bearing error under two degrees on a known source and the resolution limit shown against the textbook's prediction.",
    resources: [
      {
        title: "Adaptive Filter Theory",
        url: "https://www.pearson.com/en-us/subject-catalog/p/adaptive-filter-theory/P200000003309",
        note: "Simon Haykin, fifth edition. The adaptive filtering reference, with computer experiments in every chapter.",
      },
      {
        title: "Optimal State Estimation",
        url: "https://academic.csuohio.edu/simon-daniel/state-estimation/",
        note: "Dan Simon. Kalman, H-infinity and nonlinear filters with the derivations in full and the code online.",
      },
      {
        title: "Optimum Array Processing",
        url: "https://www.wiley.com/en-us/Optimum+Array+Processing:+Part+IV+of+Detection,+Estimation,+and+Modulation+Theory-p-9780471463832",
        note: "Harry L. Van Trees. Part four of Detection, Estimation and Modulation Theory; the array processing reference.",
      },
      {
        title: "Spectral Analysis of Signals",
        url: "https://user.it.uu.se/~ps/SAS-new.pdf",
        note: "Free, from the authors. Stoica and Moses. Parametric and nonparametric spectral estimation, from the people who did most of it.",
      },
      {
        title: "Estimation with Applications to Tracking and Navigation",
        url: "https://www.wiley.com/en-us/Estimation+with+Applications+to+Tracking+and+Navigation%3A+Theory+Algorithms+and+Software-p-9780471416555",
        note: "Bar-Shalom, Li and Kirubarajan. The tracking reference, for the nonlinear filtering task once Simon has been read.",
      },
      {
        title: "KrakenSDR",
        url: "https://www.krakenrf.com/",
        note: "A five-channel coherent receiver, if the array is to be radio rather than acoustic.",
      },
    ],
    practice: [
      {
        title: "Haykin's computer experiments",
        url: "https://www.pearson.com/en-us/subject-catalog/p/adaptive-filter-theory/P200000003309",
        note: "Each chapter's experiments have published results. Reproduce them; log the largest gap between yours and the book's.",
      },
      {
        title: "Kalman and Bayesian Filters in Python, nonlinear chapters",
        url: "https://rlabbe.github.io/Kalman-and-Bayesian-Filters-in-Python/",
        note: "Chapters 10 to 12 cover the extended, unscented and particle filters with exercises. Do them blind; log which needed the solution.",
      },
    ],
  },
  {
    id: "papers",
    title: "Papers, reproduction and the learned bridge",
    weeks: "Months 30 to 36",
    why: "Past this point the reading is papers and the practice is reproducing them, which is the fastest way to find out where a method quietly cheats. The bridge to machine learning is here too: a convolutional network is a learned filter bank, and knowing that is what tells you when the classical method wins.",
    tasks: [
      {
        id: "papers-magazine",
        text: "IEEE Signal Processing Magazine every issue, and the tables of contents of the Transactions every month. One paper a week read in full, with a one-paragraph summary in the log.",
      },
      {
        id: "papers-reproduce",
        text: "Reproduce three papers from the Transactions, one a quarter, to the authors' reported numbers or to a documented reason why not. Public code and a write-up for each.",
      },
      {
        id: "papers-learned",
        text: "On one real task, keyword spotting or modulation classification: a hand-designed front end against a learned one, a mel filter bank against a convolutional first layer. Measure both, then plot the learned filters and say what they learned.",
      },
      {
        id: "papers-databook",
        text: "Brunton and Kutz for the data-driven side: the singular value decomposition, proper orthogonal decomposition, dynamic mode decomposition and sparse regression, each applied to a signal from the earlier blocks.",
      },
      {
        id: "papers-broken",
        text: "One project where the textbook assumptions fail: nonuniform sampling, a drifting clock between transmitter and receiver, or a nonlinear amplifier. Characterise the failure, then fix it.",
      },
      {
        id: "papers-teach",
        text: "Teach one block from each stage to someone at the stage below it, with a drawing and a plot. If they cannot do the block's gate afterwards, the teaching failed.",
      },
      {
        id: "papers-publish",
        text: "Submit one reproduction to ReScience C, or one result to a signal processing workshop. Peer review is the gate nobody can grade themselves.",
      },
    ],
    gate: "Three published papers reproduced to within the authors' reported numbers, or a documented reason why they cannot be, each with public code and a write-up, in twelve months.",
    resources: [
      {
        title: "IEEE Signal Processing Magazine",
        url: "https://signalprocessingsociety.org/publications-resources/ieee-signal-processing-magazine",
        note: "Tutorials and surveys, written to be read. The monthly reading.",
      },
      {
        title: "IEEE Transactions on Signal Processing",
        url: "https://signalprocessingsociety.org/publications-resources/ieee-transactions-signal-processing",
        note: "The journal of record. The papers to reproduce come from here.",
      },
      {
        title: "Data-Driven Science and Engineering",
        url: "https://www.databookuw.com/",
        note: "Brunton and Kutz, second edition. The bridge from signal processing to machine learning, with the lectures free on the site.",
      },
      {
        title: "Machine Learning: A Bayesian and Optimization Perspective",
        url: "https://shop.elsevier.com/books/machine-learning/theodoridis/978-0-12-818803-3",
        note: "Sergios Theodoridis, second edition. Machine learning written by a signal processing professor in the language of this plan: adaptive filters, sparsity and Bayesian estimation first.",
      },
      {
        title: "Deep Learning",
        url: "https://www.deeplearningbook.org/",
        note: "Free. Goodfellow, Bengio and Courville. Chapter 9, on convolutional networks, is the one to read as a signal processing text.",
      },
    ],
    practice: [
      {
        title: "ReScience C",
        url: "https://rescience.github.io/",
        note: "A peer-reviewed journal of reproductions. A submission accepted there is the block's gate graded by someone else; log the review.",
      },
      {
        title: "IEEE ICASSP",
        url: "https://ieeeicassp.org/",
        note: "The field's annual conference, each May. Attending is the reading; a workshop submission is the score. Log either.",
      },
    ],
  },
];

export const stages: Stage[] = [
  {
    id: "beginner",
    title: "Beginner",
    weeks: "Months 1 to 6",
    why: "The goal is intuition, not proofs: to read a spectrum and predict what a filter will do before running it. Everything is done in code against real signals, and the mathematics is only what the code needs.",
    exit: "You leave when aliasing, leakage and phase distortion are things you have caused on purpose, fixed, and can explain with a plot.",
    blocks: beginner,
  },
  {
    id: "journeyman",
    title: "Journeyman",
    weeks: "Months 7 to 18",
    why: "The goal is rigor, and the ability to design rather than apply. This is where most people stall, because the mathematics gets real and the problem sets are long. The two Oppenheim books are the spine, with their problems done, and the master's courses land here.",
    exit: "You leave when you can take a vague requirement, choose the transform and the filter structure, justify the choice on paper, and predict the failure modes before testing.",
    blocks: journeyman,
  },
  {
    id: "expert",
    title: "Expert",
    weeks: "Months 19 to 36",
    why: "The goal is inventing methods and knowing where the standard tools break. The reading turns into papers, the problems into reproductions, and the signals into ones the textbook assumptions fail on: nonstationary, nonlinear, nonuniformly sampled, on hardware with a drifting clock.",
    exit: "You leave when your first instinct about a new problem is usually right, you know which of your instincts are unreliable, and you can explain the tradeoff to someone at each stage below you.",
    blocks: expert,
  },
];

/** Every block in the plan, in the order the page shows them. */
export const plan: Block[] = stages.flatMap((stage) => stage.blocks);

/** Every task in the plan, in the order the page shows them. */
export const allTasks = tasksOf(plan);

export const bookshelf: Book[] = [
  {
    title: "The Scientist and Engineer's Guide to Digital Signal Processing",
    authors: "Steven W. Smith",
    level: "beginner",
    url: "https://www.dspguide.com/",
    free: true,
    why: "The first book. No proofs, every idea shown, every chapter something you can run the same evening.",
  },
  {
    title: "Think DSP",
    authors: "Allen B. Downey",
    level: "beginner",
    url: "https://greenteapress.com/wp/think-dsp/",
    free: true,
    why: "The same ground as Smith in Python notebooks, code before mathematics. Read the two together.",
  },
  {
    title: "Introduction to Digital Filters with Audio Applications",
    authors: "Julius O. Smith III",
    level: "beginner",
    url: "https://ccrma.stanford.edu/~jos/filters/",
    free: true,
    why: "Poles, zeros and the z-transform explained better than anywhere else, with audio to hear the difference.",
  },
  {
    title: "Understanding Digital Signal Processing",
    authors: "Richard G. Lyons",
    edition: "third edition",
    level: "beginner",
    url: "https://www.pearson.com/en-us/subject-catalog/p/Lyons-Understanding-Digital-Signal-Processing-3rd-Edition/P200000000443",
    free: false,
    why: "The practitioner's book: what the equations mean, and the tricks that are in no other textbook. The one to buy first.",
  },
  {
    title: "Signals and Systems",
    authors: "Alan V. Oppenheim and Alan S. Willsky",
    edition: "second edition",
    level: "journeyman",
    url: "https://www.pearson.com/en-us/subject-catalog/p/signals-and-systems/P200000003155/9780138147570",
    free: false,
    why: "The standard undergraduate text, and MIT 6.003 follows it. The problems are the reason.",
  },
  {
    title: "Signals and Systems: Theory and Applications",
    authors: "Fawwaz T. Ulaby and Andrew E. Yagle",
    edition: "second edition",
    level: "journeyman",
    url: "https://ss2-2e.eecs.umich.edu/",
    free: true,
    why: "Michigan's free signals and systems text, with solutions on the site. The substitute for Oppenheim and Willsky if the paid book is out of reach.",
  },
  {
    title: "Schaum's Outline of Signals and Systems",
    authors: "Hwei P. Hsu",
    edition: "fourth edition",
    level: "journeyman",
    url: "https://www.mheducation.com/highered/mhp/product/schaum-s-outline-signals-systems-fourth-edition.html",
    free: false,
    why: "Hundreds of solved problems. The drill book, not the textbook.",
  },
  {
    title: "Discrete-Time Signal Processing",
    authors: "Alan V. Oppenheim and Ronald W. Schafer",
    edition: "third edition",
    level: "journeyman",
    url: "https://ocw.mit.edu/courses/res-6-dtsp-discrete-time-signal-processing/",
    free: true,
    why: "The professional reference. Every practitioner has it, the free MIT lectures are by its author, and since 2026 the book itself is free on OpenCourseWare.",
  },
  {
    title:
      "Digital Signal Processing: Principles, Algorithms, and Applications",
    authors: "John G. Proakis and Dimitris G. Manolakis",
    edition: "fifth edition",
    level: "journeyman",
    url: "https://www.pearson.com/en-us/subject-catalog/p/digital-signal-processing-principles-algorithms-and-applications/P200000003415/9780137348657",
    free: false,
    why: "The other standard text. More problems than Oppenheim and Schafer, and Radke's free lectures follow it.",
  },
  {
    title: "Mathematics of the Discrete Fourier Transform",
    authors: "Julius O. Smith III",
    level: "journeyman",
    url: "https://ccrma.stanford.edu/~jos/mdft/",
    free: true,
    why: "The DFT with every proof, from complex numbers up. The rigorous companion to the beginner stage.",
  },
  {
    title:
      "Fundamentals of Statistical Signal Processing, Volume I: Estimation Theory",
    authors: "Steven M. Kay",
    level: "journeyman",
    url: "https://www.amazon.com/Fundamentals-Statistical-Signal-Processing-Estimation/dp/0133457117",
    free: false,
    why: "Estimation done once and properly: bounds, maximum likelihood, least squares, Bayesian. Every later block cites it.",
  },
  {
    title:
      "Fundamentals of Statistical Signal Processing, Volume II: Detection Theory",
    authors: "Steven M. Kay",
    level: "journeyman",
    url: "https://www.amazon.com/Fundamentals-Statistical-Signal-Processing-Detection/dp/013504135X",
    free: false,
    why: "Detection with the same care as the first volume. Read after it.",
  },
  {
    title: "Intuitive Probability and Random Processes using MATLAB",
    authors: "Steven M. Kay",
    level: "journeyman",
    url: "https://link.springer.com/book/10.1007/b104645",
    free: false,
    why: "The probability the two volumes assume, taught by the same hand. Skip it if Stat 110 and a random processes course are already done.",
  },
  {
    title: "Kalman and Bayesian Filters in Python",
    authors: "Roger R. Labbe Jr.",
    level: "journeyman",
    url: "https://rlabbe.github.io/Kalman-and-Bayesian-Filters-in-Python/",
    free: true,
    why: "The Kalman filter built up in notebooks from the one-dimensional case. The best free thing on the subject.",
  },
  {
    title: "PySDR: A Guide to SDR and DSP using Python",
    authors: "Marc Lichtman",
    level: "journeyman",
    url: "https://pysdr.org/",
    free: true,
    why: "Applied signal processing against a real receiver, every idea with code. The radio block's text.",
  },
  {
    title: "Signal Processing for Communications",
    authors: "Paolo Prandoni and Martin Vetterli",
    level: "journeyman",
    url: "https://www.sp4comm.org/",
    free: true,
    why: "The EPFL courses' textbook, ending in a modem built from the theory. The bridge between the two Oppenheim books and radio.",
  },
  {
    title: "Digital Communications: A Discrete-Time Approach",
    authors: "Michael Rice",
    level: "journeyman",
    url: "https://www.pearson.com/en-us/subject-catalog/p/digital-communications-a-discrete-time-approach/P200000003211",
    free: false,
    why: "Synchronisation as signal processing. The book to read before writing a receiver's timing and carrier recovery.",
  },
  {
    title: "Foundations of Signal Processing",
    authors: "Martin Vetterli, Jelena Kovačević and Vivek K Goyal",
    level: "expert",
    url: "https://www.fourierandwavelets.org/",
    free: true,
    why: "The subject rebuilt from Hilbert spaces and approximation. The book that turns a journeyman's toolbox into a theory.",
  },
  {
    title: "A Wavelet Tour of Signal Processing: The Sparse Way",
    authors: "Stéphane Mallat",
    edition: "third edition",
    level: "expert",
    url: "https://wavelet-tour.github.io/",
    free: false,
    why: "Wavelets, time-frequency and sparsity from the person who did much of it.",
  },
  {
    title: "A Mathematical Introduction to Compressive Sensing",
    authors: "Simon Foucart and Holger Rauhut",
    level: "expert",
    url: "https://link.springer.com/book/10.1007/978-0-8176-4948-7",
    free: false,
    why: "Compressed sensing with the proofs. Read after the survey, when the phase transition experiment raises the question of why.",
  },
  {
    title: "Linear Algebra and Learning from Data",
    authors: "Gilbert Strang",
    level: "expert",
    url: "https://math.mit.edu/~gs/learningfromdata/",
    free: false,
    why: "The linear algebra of the expert stage: the singular value decomposition, low rank, and the bases you learn from data.",
  },
  {
    title: "Adaptive Filter Theory",
    authors: "Simon Haykin",
    edition: "fifth edition",
    level: "expert",
    url: "https://www.pearson.com/en-us/subject-catalog/p/adaptive-filter-theory/P200000003309",
    free: false,
    why: "The adaptive filtering reference, with computer experiments to reproduce.",
  },
  {
    title: "Optimal State Estimation",
    authors: "Dan Simon",
    level: "expert",
    url: "https://academic.csuohio.edu/simon-daniel/state-estimation/",
    free: false,
    why: "Kalman, H-infinity and nonlinear filters with the derivations in full and the code online. The Kalman book after Labbe.",
  },
  {
    title: "Estimation with Applications to Tracking and Navigation",
    authors: "Yaakov Bar-Shalom, X. Rong Li and Thiagalingam Kirubarajan",
    level: "expert",
    url: "https://www.wiley.com/en-us/Estimation+with+Applications+to+Tracking+and+Navigation%3A+Theory+Algorithms+and+Software-p-9780471416555",
    free: false,
    why: "The tracking reference: nonlinear filtering and data association, for when the Kalman filter meets a real target.",
  },
  {
    title: "Optimum Array Processing",
    authors: "Harry L. Van Trees",
    level: "expert",
    url: "https://www.wiley.com/en-us/Optimum+Array+Processing:+Part+IV+of+Detection,+Estimation,+and+Modulation+Theory-p-9780471463832",
    free: false,
    why: "The array processing reference. Fourteen hundred pages; the block reads four chapters of it.",
  },
  {
    title: "Spectral Analysis of Signals",
    authors: "Petre Stoica and Randolph Moses",
    level: "expert",
    url: "https://user.it.uu.se/~ps/SAS-new.pdf",
    free: true,
    why: "Spectral estimation, parametric and not, by the authors of most of it. Free from the authors.",
  },
  {
    title: "Data-Driven Science and Engineering",
    authors: "Steven L. Brunton and J. Nathan Kutz",
    edition: "second edition",
    level: "expert",
    url: "https://www.databookuw.com/",
    free: false,
    why: "The bridge to machine learning, with every chapter's lectures free on the site.",
  },
  {
    title: "Machine Learning: A Bayesian and Optimization Perspective",
    authors: "Sergios Theodoridis",
    edition: "second edition",
    level: "expert",
    url: "https://shop.elsevier.com/books/machine-learning/theodoridis/978-0-12-818803-3",
    free: false,
    why: "Machine learning from the signal processing side: adaptive filters, sparsity and Bayesian estimation before anything is called a network.",
  },
  {
    title: "Deep Learning",
    authors: "Ian Goodfellow, Yoshua Bengio and Aaron Courville",
    level: "expert",
    url: "https://www.deeplearningbook.org/",
    free: true,
    why: "Chapter 9 on convolutional networks, read as a signal processing text: a learned filter bank, and when a designed one still wins.",
  },
];
