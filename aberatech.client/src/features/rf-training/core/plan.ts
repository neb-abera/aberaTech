/**
 * The field communications curriculum, as data.
 *
 * The page renders this and nothing else, so the plan can be tested as a
 * plan: every task has an address, every link is https, every block ends in
 * a gate that can be timed. plan.test.ts holds it to those rules and to one
 * more, that the text stays on its own subject, so an edit cannot quietly
 * turn a public page into something it was not meant to be.
 */

export interface Resource {
  title: string;
  url: string;
  /** What to do with it, when the title alone does not say. */
  note?: string;
}

export interface Task {
  /** Stable, unique across the whole plan: it is the key progress is stored under. */
  id: string;
  text: string;
}

export interface Block {
  id: string;
  title: string;
  /** When in the year this block runs. */
  weeks: string;
  /** Why the block exists, in one or two sentences. */
  why: string;
  tasks: Task[];
  /** A pass or fail test with a clock on it. The block is done when this is. */
  gate: string;
  /** Reading and reference. */
  resources: Resource[];
  /**
   * Things that score you: practice exams, drills, sites that show where
   * your signal was heard. Their results go in the gate log's note.
   */
  practice: Resource[];
}

export interface CadenceItem {
  label: string;
  detail: string;
}

export interface GearItem {
  item: string;
  purpose: string;
  /** Rough, in US dollars, for a budget rather than a quote. */
  cost: string;
}

export const copy = {
  intro:
    "A one-year plan for getting good at field radio: wire antennas and HF, networks, mesh, spectrum, drones and power. I wrote it for myself and I am working through it. The blocks line up with the electrical engineering courses I am taking next, so the field work and the classroom feed each other.",
  note: "The checklist, drill history and gate log are mine. Signed out, the page is read-only.",
  scoring:
    "Blocks are scored on gates and clocks, not hours. A block is done when its gate passes, timed, outdoors.",
  cards:
    "Cards to carry until the drill makes them unnecessary. They print together with the plan template.",
  practice:
    "Each block lists practice that scores you as well as reading. Those sites cannot report back here, so I write their results in the gate log: an exam percentage, a drill time, where a signal was heard.",
};

export const rules: string[] = [
  "Log everything. The plan is scored on the log, not on how it felt.",
  "Outdoors beats the bench. One contact from a hilltop on battery is worth three from a desk.",
  "Every gate has a clock. If it is not timed, it is not a gate, it is a hobby.",
  "Teach it to keep it. A skill you cannot explain with a drawing is a skill you half have.",
  "Never let the arithmetic be the thing that fails. Decibels, wavelengths and subnet masks are drilled until they are reflexes.",
];

export const cadence: CadenceItem[] = [
  {
    label: "Study",
    detail:
      "Three hours a week from the block's reading, in sittings of at least an hour.",
  },
  {
    label: "On the air",
    detail:
      "Two hours a week transmitting, from block two onward. Logged contacts, not listening.",
  },
  {
    label: "Drills",
    detail:
      "Ten minutes a day on the drill at the top of this page: decibels, wavelengths, antenna lengths, Ohm's law, subnetting. Timed, scored, kept.",
  },
  {
    label: "Field build",
    detail:
      "One outdoor exercise a month from week twelve, carrying the whole kit. Twenty four hours minimum, seventy two preferred.",
  },
  {
    label: "Review",
    detail:
      "Sunday evening: read the week's log, score the drills, write the next week's three priorities.",
  },
];

export const gear: GearItem[] = [
  {
    item: "Portable HF transceiver, 20 W class",
    purpose: "Every antenna and propagation block; runs from a small battery",
    cost: "450 to 650",
  },
  {
    item: "Vector network analyzer (NanoVNA)",
    purpose: "Sweeping and trimming every antenna you build",
    cost: "60",
  },
  {
    item: "Wire, coax, insulators, a 1:1 and a 49:1 balun",
    purpose: "Dipoles, inverted Vs, end-fed half-waves and NVIS antennas",
    cost: "80",
  },
  {
    item: "12 V lithium iron phosphate battery, 20 Ah, and charger",
    purpose: "Field power for the radio and everything charged from it",
    cost: "150",
  },
  {
    item: "Software-defined radio receiver (RTL-SDR) with antenna kit",
    purpose: "Watching the spectrum; identifying and locating signals",
    cost: "50",
  },
  {
    item: "Four LoRa mesh nodes",
    purpose: "The mesh block, then every field exercise after it",
    cost: "120",
  },
  {
    item: "Travel router, cellular modem, two used managed switches",
    purpose: "The field network and the VLAN lab",
    cost: "150",
  },
  {
    item: "Directional VHF antenna and a step attenuator",
    purpose: "Direction finding",
    cost: "40",
  },
  {
    item: "Sub-250 gram quadcopter",
    purpose: "The drone block; its links are also a target on the waterfall",
    cost: "400",
  },
  {
    item: "100 W folding solar panel and charge controller",
    purpose: "The seventy two hour power budget",
    cost: "200",
  },
];

export const plan: Block[] = [
  {
    id: "licence",
    title: "Licence and the arithmetic",
    weeks: "Weeks 1 to 4",
    why: "The licence itself is paperwork. The exam pools are the point: they force the mathematics and the regulations that every later block assumes you already have.",
    tasks: [
      {
        id: "licence-exams",
        text: "Pass the amateur radio Technician and General exams in a single sitting.",
      },
      {
        id: "licence-drills",
        text: "Drill until automatic: decibels to power ratios, wavelength from frequency, half-wave dipole length (468 divided by the frequency in megahertz gives feet), Ohm's law and power, subnet masks from prefix lengths.",
      },
      {
        id: "licence-objectives",
        text: "Read the Network+ exam objectives end to end and mark every term you cannot define in a sentence.",
      },
      {
        id: "licence-logbook",
        text: "Set up the logbook and the study log. Everything after this is scored from them.",
      },
    ],
    gate: "General licence in hand, and fifty mixed arithmetic problems in ten minutes with no errors.",
    resources: [
      {
        title: "HamStudy",
        url: "https://hamstudy.org/",
        note: "The question pools with spaced repetition. Free.",
      },
      {
        title: "ARRL licensing, education and training",
        url: "https://www.arrl.org/licensing-education-training",
        note: "Where the licence manuals and local exam sessions are listed.",
      },
      {
        title: "CompTIA Network+ exam objectives",
        url: "https://www.comptia.org/certifications/network",
      },
    ],
    practice: [
      {
        title: "HamStudy practice exams",
        url: "https://hamstudy.org/",
        note: "Study mode until it reports 85 percent seen and aced, then timed practice exams only. Log the score.",
      },
      {
        title: "HamExam",
        url: "https://hamexam.org/",
        note: "A second question pool, for a cold check the week before the session.",
      },
      {
        title: "Anki",
        url: "https://apps.ankiweb.net/",
        note: "Write your own cards for the formulas and the regulations. Spaced repetition is the answer to passive reading for the whole plan.",
      },
    ],
  },
  {
    id: "wire",
    title: "On the air, on wire",
    weeks: "Weeks 5 to 12",
    why: "Nothing learned at a keyboard substitutes for having tuned a wire in the rain. This block is the one that no amount of software background covers.",
    tasks: [
      {
        id: "wire-kit",
        text: "Buy the transceiver, the analyzer, a spool of wire, coax and the battery. Nothing else yet.",
      },
      {
        id: "wire-build",
        text: "Build and tune a half-wave dipole, an inverted V and an end-fed half-wave. Sweep each with the analyzer and log the standing wave ratio before and after every trim.",
      },
      {
        id: "wire-ocf",
        text: "Build a half-wave dipole fed 14 percent from its centre through a 4:1 balun, and confirm on the analyzer that it works on its even harmonics too. Compute the feed point in feet and inches before you cut.",
      },
      {
        id: "wire-longwire",
        text: "Build a long wire, several wavelengths on the band in use, fed at one end against a counterpoise. Log which directions it favours.",
      },
      {
        id: "wire-terminated",
        text: "Build the two terminated directional wires: a sloping vee and a vertical half-rhombic, each ended in a 400 to 600 ohm non-inductive resistor. Point them by compass at a distant station and compare against the dipole on the same hour.",
      },
      {
        id: "wire-multimeter",
        text: "With a multimeter: zero it, prove the continuity of every antenna wire and feedline, and read the battery's voltage, inside ten minutes. Do it before every field build.",
      },
      {
        id: "wire-nvis",
        text: "Build a near vertical incidence skywave (NVIS) antenna low to the ground and make contacts inside three hundred miles on 40 and 80 meters, the band that is hard to reach any other way.",
      },
      {
        id: "wire-contacts",
        text: "Make one hundred logged contacts from at least five outdoor locations on battery power.",
      },
      {
        id: "wire-digital",
        text: "Run JS8Call and Winlink over HF. Send yourself an email over the radio with no internet in the chain.",
      },
      {
        id: "wire-propagation",
        text: "Keep a propagation log: time, band, solar flux, what you could and could not hear. Learn the maximum and lowest usable frequencies from your own data before reading about them.",
      },
    ],
    gate: "Given a frequency and an azimuth to the distant station: compute the length, cut the wire to within three inches of it, erect it broadside to that azimuth by compass, connect the radio and log a contact, all inside thirty minutes, in the dark, by headlamp. Then the same with the off-centre-fed and a terminated wire.",
    resources: [
      {
        title: "Near vertical incidence skywave",
        url: "https://en.wikipedia.org/wiki/Near_vertical_incidence_skywave",
        note: "The short explanation. Then the ARRL Antenna Book chapter on it.",
      },
      {
        title: "Parks on the Air",
        url: "https://parksontheair.com/",
        note: "The easiest way to make outdoor, battery-powered contacts a habit.",
      },
      { title: "JS8Call", url: "https://js8call.com/" },
      { title: "Winlink", url: "https://winlink.org/" },
    ],
    practice: [
      {
        title: "PSKReporter",
        url: "https://pskreporter.info/pskmap.html",
        note: "Where your digital signal was heard and how strong. Change one thing about the antenna, transmit again, compare. Log the difference.",
      },
      {
        title: "Reverse Beacon Network",
        url: "https://www.reversebeacon.net/",
        note: "The same for CW and some digital modes: skimmers report your signal-to-noise ratio.",
      },
      {
        title: "VOACAP online",
        url: "https://www.voacap.com/hf/",
        note: "Predict a path for a band and hour, then try it. Score your predictions against the log.",
      },
      {
        title: "EZNEC",
        url: "https://www.eznec.com/",
        note: "Free antenna modelling. Model it, predict the impedance and pattern, then build it and measure. The gap is the lesson.",
      },
      {
        title: "WebSDR",
        url: "https://websdr.org/",
        note: "Remote receivers in a browser. Practise tuning, reading a waterfall and hearing NVIS against skip before the rig arrives.",
      },
      {
        title: "KiwiSDR public receivers",
        url: "http://kiwisdr.com/public/",
        note: "Hundreds more remote receivers; listen for your own signal from another state.",
      },
    ],
  },
  {
    id: "networks",
    title: "Networks",
    weeks: "Weeks 13 to 24",
    why: "Every modern radio is a network node. The operator who cannot subnet is the operator who cannot pass data, whatever the radio can do.",
    tasks: [
      {
        id: "networks-ccna",
        text: "Work through the Cisco Certified Network Associate (CCNA) official certification guide, and lab every chapter in Packet Tracer as you go.",
      },
      {
        id: "networks-subnetting",
        text: "Subnet by hand daily until twenty problems take five minutes.",
      },
      {
        id: "networks-field",
        text: "Build a field network from a pack: travel router, cellular modem, laptop, two phones. Static addressing where it matters, DHCP where it does not, local DNS, and a VPN back to the house.",
      },
      {
        id: "networks-vlan",
        text: "Configure a VLAN-segmented network on two real, used managed switches, with routing between the segments.",
      },
      {
        id: "networks-exam",
        text: "Sit the CCNA. If the calendar will not allow it, sit Network+ instead and keep the CCNA as the next block's stretch.",
      },
    ],
    gate: "CCNA passed, or Network+ passed plus a documented field network that is up and passing traffic fifteen minutes after the pack is opened.",
    resources: [
      {
        title: "Cisco Packet Tracer",
        url: "https://www.netacad.com/cisco-packet-tracer",
        note: "Free. Every lab in the guide runs in it.",
      },
      {
        title: "CCNA exam topics",
        url: "https://learningnetwork.cisco.com/s/ccna-exam-topics",
      },
      {
        title: "Professor Messer",
        url: "https://www.professormesser.com/",
        note: "The free Network+ video course, if that is the exam you sit.",
      },
    ],
    practice: [
      {
        title: "Subnetting Practice",
        url: "https://subnettingpractice.com/",
        note: "Timed subnet drills with instant marking. Log the time for twenty.",
      },
      {
        title: "subnetting.net",
        url: "https://subnetting.net/",
        note: "A second drill site with a different question style.",
      },
      {
        title: "Jeremy's IT Lab",
        url: "https://www.youtube.com/@JeremysITLab",
        note: "The free CCNA course with a Packet Tracer lab file for every lesson. The labs are the practice; the videos are not.",
      },
    ],
  },
  {
    id: "mesh",
    title: "Mesh and the shared map",
    weeks: "Weeks 25 to 32",
    why: "Position and text over a mesh with nothing underneath it is the baseline for a small group outdoors now. It is also the block where the networks block starts paying rent.",
    tasks: [
      {
        id: "mesh-atak",
        text: "Install the civilian Android Team Awareness Kit (ATAK) on two devices. Learn markers, routes, chat and data packages until they are muscle memory.",
      },
      {
        id: "mesh-server",
        text: "Stand up a TAK server on a single-board computer or a small virtual machine and connect both devices with certificates, not passwords.",
      },
      {
        id: "mesh-nodes",
        text: "Build a four-node LoRa mesh. Measure range and hop behavior in woods and in a built-up area, and feed the node positions into the shared map.",
      },
      {
        id: "mesh-card",
        text: "Write a one-page setup card for the mesh that a friend can follow without you standing there.",
      },
    ],
    gate: "Two people three kilometers apart with no cellular service, exchanging position and text through the mesh into one shared map.",
    resources: [
      {
        title: "TAK, civilian release",
        url: "https://tak.gov/",
        note: "The Android app and the server both come from here.",
      },
      {
        title: "Meshtastic documentation",
        url: "https://meshtastic.org/docs/",
        note: "The LoRa mesh, and the plugin that puts it on the ATAK map.",
      },
    ],
    practice: [
      {
        title: "Meshtastic documentation",
        url: "https://meshtastic.org/docs/",
        note: "No site scores a mesh. Time yourself from a cold pack to positions on the shared map, and log it at the gate.",
      },
    ],
  },
  {
    id: "spectrum",
    title: "Spectrum",
    weeks: "Weeks 33 to 40",
    why: "You can only fix what you can see. A software-defined receiver turns the spectrum into something you can watch, and watching it is how you learn what a healthy link and a broken one look like.",
    tasks: [
      {
        id: "spectrum-identify",
        text: "With the RTL-SDR and a tuned antenna, identify twenty signal types by eye from the waterfall, checked against the Signal Identification Wiki.",
      },
      {
        id: "spectrum-gnuradio",
        text: "In GNU Radio, build a broadcast FM receiver from blocks, then a decoder for one digital mode, and understand every block in the chain.",
      },
      {
        id: "spectrum-df",
        text: "With a directional antenna and an attenuator, enter an amateur radio direction finding event, or hide a beacon and run your own.",
      },
      {
        id: "spectrum-interference",
        text: "Learn what interference looks like on the waterfall: broadband noise, a carrier parked on the frequency, sweeping and pulsed sources. When it lands on your own link, recognise it, note the time and the shape, change something (band, antenna, power, timing) and get the traffic through anyway.",
      },
      {
        id: "spectrum-source",
        text: "Log every interference problem on your own field network and find its physical source. Write down how you found it.",
      },
    ],
    gate: "Locate a hidden transmitter inside one hour in terrain you have not walked before.",
    resources: [
      {
        title: "RTL-SDR quick start",
        url: "https://www.rtl-sdr.com/rtl-sdr-quick-start-guide/",
      },
      {
        title: "Signal Identification Wiki",
        url: "https://www.sigidwiki.com/",
      },
      {
        title: "GNU Radio",
        url: "https://www.gnuradio.org/",
        note: "The tutorials are linked from the front page, and they are the course.",
      },
      {
        title: "ARRL direction finding",
        url: "https://www.arrl.org/direction-finding",
        note: "Radio direction finding: antennas to build, events to enter.",
      },
    ],
    practice: [
      {
        title: "PySDR",
        url: "https://pysdr.org/",
        note: "A free textbook where every idea comes with Python you run against the receiver. Do every exercise; do not read past one.",
      },
      {
        title: "Signal Identification Wiki",
        url: "https://www.sigidwiki.com/wiki/Signal_Identification_Guide",
        note: "Audio and waterfall samples for every signal. Make flashcards of the images, answer on the back, and drill them in Anki.",
      },
      {
        title: "WebSDR",
        url: "https://websdr.org/",
        note: "Live signal identification against the wiki, from any receiver in the world, any hour.",
      },
      {
        title: "ARDF USA",
        url: "https://ardf.us/",
        note: "Direction finding events. An event is a scored gate run by somebody else.",
      },
    ],
  },
  {
    id: "power",
    title: "Drones and power",
    weeks: "Weeks 41 to 48",
    why: "Small uncrewed aircraft and a power budget are part of field communications now, not next to it. The drone's links are also the most instructive thing you will ever watch on a waterfall.",
    tasks: [
      {
        id: "power-part107",
        text: "Pass the Federal Aviation Administration Part 107 remote pilot exam.",
      },
      {
        id: "power-fly",
        text: "Fly a sub-250 gram quadcopter for twenty logged hours: orbits, return-to-home failures, flying by the video feed alone. Find its control and video links on the SDR waterfall and watch what distance does to them.",
      },
      {
        id: "power-budget",
        text: "Write a seventy two hour power budget for the whole kit: radio, mesh nodes, phones, laptop, drone. Build the solar and battery set that meets it.",
      },
      {
        id: "power-chemistry",
        text: "Learn the three battery chemistries you will actually carry: charge profiles, behavior in the cold, and the rules for carrying them on aircraft.",
      },
    ],
    gate: "Seventy two hours in the field on the budget you wrote. Everything charged at the end, nothing dead in the middle.",
    resources: [
      {
        title: "FAA: become a certificated remote pilot",
        url: "https://www.faa.gov/uas/commercial_operators/become_a_drone_pilot",
      },
    ],
    practice: [
      {
        title: "Pilot Institute Part 107 practice test",
        url: "https://pilotinstitute.com/part-107-practice-test/",
        note: "Free, scored, and every answer explained. Log the percentage.",
      },
      {
        title: "Liftoff",
        url: "https://www.liftoff-game.com/",
        note: "The simulator FPV pilots train in. Simulator hours count in the log like real ones; say which.",
      },
      {
        title: "Velocidrone",
        url: "https://www.velocidrone.com/",
        note: "The other simulator. Either one; pick the one that runs on your machine.",
      },
    ],
  },
  {
    id: "field",
    title: "Field exercises",
    weeks: "Monthly from week 12, all year",
    why: "A skill you have not used under a time limit outdoors is a skill you have read about. The monthly exercise is where the other six blocks are found out.",
    tasks: [
      {
        id: "field-monthly",
        text: "Once a month, twenty four to seventy two hours outdoors carrying the whole kit: HF, mesh, network, power. Set the distance and the load beforehand and do not renegotiate them on the day.",
      },
      {
        id: "field-plan",
        text: "Before each one, write the communications plan from the template on this page: primary, alternate, contingency and emergency means for every link, a net diagram, the scheduled contact windows in Zulu, the frequencies by time of day, and the power plan. Four hours from scenario to finished plan is the standard.",
      },
      {
        id: "field-site",
        text: "Choose the transmission site from the map before you walk: line of sight to the far station for ground wave, or, for sky wave, ridge lines in the direction of transmission no higher than half the take-off angle you need, with room for the antenna and its supports. Then confirm it on the ground.",
      },
      {
        id: "field-leave",
        text: "Leave the site as you found it: wire, insulators, stakes, tape and batteries all counted back into the pack. A dropped length of wire is a failed exercise.",
      },
      {
        id: "field-teach",
        text: "Teach one friend with no technical background to put up the antenna and make a contact, using a drawing and plain sentences. If they cannot do it, the teaching is what failed.",
      },
      {
        id: "field-events",
        text: "Enter ARRL Field Day, Winter Field Day and at least one park activation, and score each against the last.",
      },
      {
        id: "field-aar",
        text: "After every exercise, one page: what failed, why, and the one thing that changes before the next one.",
      },
    ],
    gate: "Twelve logged exercises in the year, and the last one passes every earlier gate back to back in one weekend.",
    resources: [
      { title: "ARRL Field Day", url: "https://www.arrl.org/field-day" },
      { title: "Winter Field Day", url: "https://winterfieldday.org/" },
      {
        title:
          "Techniques for Tactical Radio Operations (Army Techniques Publication 6-02.53)",
        url: "https://armypubs.army.mil/",
        note: "Public doctrine. The chapters on choosing antennas, propagation and communications planning are the best free reference on the subject.",
      },
    ],
    practice: [
      {
        title: "Parks on the Air",
        url: "https://parksontheair.com/",
        note: "Scored activations. Ten contacts from the park is the standard; log the count and the time to the tenth.",
      },
      {
        title: "ARRL contests",
        url: "https://www.arrl.org/contests",
        note: "A contest weekend is a timed exercise with a scoreboard.",
      },
      {
        title: "Summits on the Air",
        url: "https://www.sotadata.org.uk/",
        note: "If the ruck should count for something too.",
      },
    ],
  },
];

/** Every task in the plan, in the order the page shows them. */
export const allTasks: Task[] = plan.flatMap((block) => block.tasks);
