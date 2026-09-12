/**
 * The quantum and post-quantum cryptography curriculum, as data.
 *
 * The page renders this and nothing else, so the plan can be tested as a
 * plan: every task has an address, every link is https, every block ends
 * in a gate with a number in it, every book on the shelf is read from
 * somewhere in the plan, and every standard in the table is current as of
 * a date the table states. plan.test.ts holds it to those rules.
 *
 * Three stages, twelve blocks, eighteen months. Classical cryptography
 * first, done properly, because post-quantum cryptography is classical
 * cryptography with different hardness assumptions and none of it makes
 * sense without the reductions. Then enough quantum mechanics to see
 * exactly what Shor's algorithm breaks and what it costs. Then the
 * post-quantum families, to the level of implementing the standards from
 * their FIPS documents alone and defending their parameters.
 */

import {
  type Block,
  type CadenceItem,
  tasksOf,
} from "../../progress/core/curriculum";

export const documentKey = "quantum-cryptography";

export interface Idea {
  name: string;
  /** The idea, in two sentences. */
  idea: string;
  /** What owning it looks like: something you can do, not something you know. */
  test: string;
}

export interface Stage {
  id: "classical" | "quantum" | "postquantum";
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

/** One row of the standards table: what it is, and where it stands. */
export interface Standard {
  name: string;
  /** What the document standardises, in a few words. */
  what: string;
  /** Final, draft, or a date it takes effect. */
  status: string;
  url: string;
}

export const levels: Record<Level, string> = {
  classical: "Classical",
  quantum: "Quantum",
  postquantum: "Post-quantum",
};

export const copy = {
  intro:
    "An eighteen-month plan from the arithmetic under RSA to the lattice mathematics under ML-KEM, written for a working cryptographic engineer about to spend their days on the post-quantum transition. Classical cryptography first and properly, then enough quantum mechanics to see exactly what Shor's algorithm breaks and what it costs, then the post-quantum families to the level of implementing the standards from their FIPS documents alone and defending their parameters.",
  note: "The checklist and the gate log are mine. Signed out, the page is read-only.",
  scoring:
    "Blocks are scored on gates, not hours. A gate is an implementation that passes the published test vectors, a proof that survives a check against the book, or a written assessment someone else has read, and the block is done when the gate passes.",
  practice:
    "Each block lists practice that scores you as well as reading: challenge sites, test vectors, problem sets with answers. Those cannot report back here, so I write their results in the gate log: a set completed, a vector suite passed, a parameter estimate reproduced.",
  ideas:
    "Eight ideas carry the whole subject; everything after is one of them applied. Each stage returns to all eight at more depth, and the second column is what owning one looks like.",
  standards:
    "The documents the transition is measured against, and where each stood in September 2026. Check the dates before quoting any of them: this table is a snapshot, and the additional-signature process in particular moves every few months.",
  bookshelf:
    "Every book the blocks read from, in one place, with what it is for. Free ones are marked, and there is one at every stage. The paid ones are worth a used copy of the edition named.",
};

export const rules: string[] = [
  "Implement from the specification alone. Every standard in the plan is written once from its FIPS or RFC with the reference code closed, then checked against the published test vectors. Reading an implementation is not the same as being able to produce one.",
  "State the assumption. Every scheme rests on a hardness assumption and every proof is a reduction to it. A claim of security that does not name what it reduces to is marketing.",
  "Run every attack you read. A break described in a paper is reproduced, in Sage or Python, on a toy parameter set, before it counts as understood.",
  "Never let the arithmetic be the thing that fails. Modular inverses, the Chinese remainder theorem, polynomial multiplication in a quotient ring and the number-theoretic transform are drilled until they are reflexes.",
  "Treat quantum-safe on a product sheet as a question, not an answer. Ask which algorithm, which parameter set, which validation, and whether the classical half of a hybrid is still there.",
  "Keep the log. A block is done when its gate passes, and the gate is scored from the log, not from how it felt.",
];

export const cadence: CadenceItem[] = [
  {
    label: "Study",
    detail:
      "Six hours a week from the block's reading, in sittings of at least an hour, with the exercises done as they come rather than saved for later.",
  },
  {
    label: "Code",
    detail:
      "Four hours a week in Python, with Sage for anything algebraic. Every scheme in the week's reading is run against real test vectors the same week.",
  },
  {
    label: "Derive",
    detail:
      "Twenty minutes a day rederiving one result from memory on paper: a reduction, a decryption correctness bound, the cost of a lattice attack. Timed, checked against the book, kept.",
  },
  {
    label: "Read the field",
    detail:
      "One paper abstract a day from the IACR ePrint listing, and the week's posts on the NIST post-quantum forum. Ten minutes; the point is to recognise names and problems, not to follow every thread.",
  },
  {
    label: "Review",
    detail:
      "Sunday evening: read the week's log, score the derivations, write the next week's three priorities.",
  },
];

export const ideas: Idea[] = [
  {
    name: "Security is a reduction",
    idea: "A scheme is secure relative to a hardness assumption, and the proof is an algorithm: anyone who breaks the scheme is turned into someone who breaks the assumption. There is no other kind of security argument.",
    test: "You write the reduction for a given scheme yourself, with the simulator and the loss stated, and can say what breaks if a hypothesis is dropped.",
  },
  {
    name: "Structure is what a quantum computer eats",
    idea: "Shor's algorithm finds hidden periods, and factoring, discrete logarithms and elliptic curves all reduce to period finding. Problems without that structure, lattices and hashes among them, only lose the square root that Grover takes.",
    test: "Shown a new hardness assumption, you say whether a quantum computer gets a polynomial or a square-root advantage against it, and why.",
  },
  {
    name: "Amplitudes interfere",
    idea: "A quantum computer is not trying every answer at once. Amplitudes can be negative, and an algorithm is a way of arranging for the wrong answers to cancel and the right one to add up.",
    test: "You explain Grover's rotation picture from memory, say why iterating past the optimum makes it worse, and why this gives a square root and no more.",
  },
  {
    name: "Information-theoretic versus computational",
    idea: "The one-time pad and quantum key distribution are secure against any computer, at the cost of a channel that must already be authenticated. Everything post-quantum is computational, secure only for as long as its assumption holds.",
    test: "You can say which half of a deployed system is which, and what the authentication in a quantum key distribution link is actually protected by.",
  },
  {
    name: "Measurement disturbs, states cannot be copied",
    idea: "Measuring a quantum state in the wrong basis changes it, and no process copies an unknown state. Between them these are the whole of why an eavesdropper on a quantum channel leaves marks.",
    test: "Given a protocol on a quantum channel, you name the measurement an attacker is forced to make and compute the error rate it leaves behind.",
  },
  {
    name: "Errors hide secrets",
    idea: "A system of linear equations is easy; the same system with a little noise added to each equation is the learning with errors problem, and nobody knows how to solve it, quantum computer or not. Every lattice standard is this idea in a polynomial ring.",
    test: "You write out ML-KEM's key generation, encapsulation and decapsulation as operations on module elements, and derive the decryption failure bound from the noise sizes.",
  },
  {
    name: "Parameters are a cryptanalysis budget",
    idea: "A parameter set is chosen by costing the best known attack and leaving a margin. For lattices that is the block size at which lattice reduction finds the short vector, and the margin is the argument that has to be defended.",
    test: "You run the lattice estimator on a scheme's parameters and reproduce the security level its designers claim, then say which attack sets it and what a better one would cost.",
  },
  {
    name: "The job is migration",
    idea: "Traffic recorded today is decrypted when the machine arrives, so key exchange had to move first and signatures follow. Most of the work is finding where cryptography is, not choosing what replaces it.",
    test: "Handed a system, you produce an inventory of its cryptography, rank what is exposed to harvest-now-decrypt-later, and put each item on the published transition timeline.",
  },
];

export const standards: Standard[] = [
  {
    name: "FIPS 203, ML-KEM",
    what: "The lattice key encapsulation mechanism, from Kyber",
    status: "Final, August 2024",
    url: "https://csrc.nist.gov/pubs/fips/203/final",
  },
  {
    name: "FIPS 204, ML-DSA",
    what: "The lattice signature, from Dilithium",
    status: "Final, August 2024",
    url: "https://csrc.nist.gov/pubs/fips/204/final",
  },
  {
    name: "FIPS 205, SLH-DSA",
    what: "The stateless hash-based signature, from SPHINCS+",
    status: "Final, August 2024",
    url: "https://csrc.nist.gov/pubs/fips/205/final",
  },
  {
    name: "FIPS 206, FN-DSA",
    what: "The compact lattice signature, from Falcon",
    status: "Draft; final expected late 2026 or 2027",
    url: "https://csrc.nist.gov/projects/post-quantum-cryptography",
  },
  {
    name: "SP 800-208",
    what: "The stateful hash-based signatures, LMS and XMSS",
    status: "Final, October 2020",
    url: "https://csrc.nist.gov/pubs/sp/800/208/final",
  },
  {
    name: "HQC",
    what: "A code-based key encapsulation mechanism, the backup to ML-KEM",
    status: "Selected March 2025; draft standard pending",
    url: "https://csrc.nist.gov/projects/post-quantum-cryptography",
  },
  {
    name: "Additional signatures",
    what: "The second signature competition, nine candidates in round three",
    status: "Round three since May 2026; standards not before 2028",
    url: "https://csrc.nist.gov/projects/pqc-dig-sig",
  },
  {
    name: "NIST IR 8547",
    what: "The transition timeline: RSA and elliptic curves deprecated 2030, disallowed 2035",
    status: "Initial public draft, November 2024",
    url: "https://csrc.nist.gov/pubs/ir/8547/ipd",
  },
  {
    name: "CNSA 2.0",
    what: "The national security systems suite and its dates, from software signing in 2025 to everything by 2035",
    status: "In force; dates are per system class",
    url: "https://media.defense.gov/2022/Sep/07/2003071834/-1/-1/0/CSA_CNSA_2.0_ALGORITHMS_.PDF",
  },
  {
    name: "X25519MLKEM768",
    what: "The hybrid key exchange browsers and servers actually deploy in TLS 1.3",
    status: "IETF draft, widely deployed",
    url: "https://datatracker.ietf.org/doc/draft-ietf-tls-ecdhe-mlkem/",
  },
];

export const stages: Stage[] = [
  {
    id: "classical",
    title: "Classical cryptography, properly",
    weeks: "Weeks 1 to 20",
    why: "Post-quantum cryptography is classical cryptography with different hardness assumptions. The reductions, the security games, the implementation discipline and the protocol mistakes are all the same, so this stage builds them from the ground up even for someone who has shipped cryptographic code. What was learned on the job is checked against the book here.",
    exit: "You can write a reduction proof for a symmetric or public-key scheme without the book open, you have implemented RSA, Diffie-Hellman and ECDSA from the definitions and broken each one through a known misuse, and you have modelled a protocol in a prover and watched it find an attack.",
    blocks: [
      {
        id: "maths",
        title: "The mathematics underneath",
        weeks: "Weeks 1 to 4",
        why: "Everything later is arithmetic in a finite structure: integers modulo a prime, polynomials modulo another polynomial, vectors over both. This block makes that arithmetic automatic, so the cryptography can be about the cryptography.",
        tasks: [
          {
            id: "maths-modular",
            text: "Work through Shoup chapters 1 to 4 and 7: divisibility, congruences, the extended Euclidean algorithm, the Chinese remainder theorem, and the structure of the multiplicative group modulo n. Do the exercises with a pen before checking.",
          },
          {
            id: "maths-fields",
            text: "Groups, rings and fields from Shoup chapters 6, 9 and 16 to 19: cyclic groups and generators, polynomial rings, quotient rings, and the construction of finite fields of prime-power order.",
          },
          {
            id: "maths-linear",
            text: "Linear algebra over a finite field: Gaussian elimination modulo a prime, kernels and images, and the number of solutions of a linear system. This is what code-based and multivariate cryptography live on.",
          },
          {
            id: "maths-probability",
            text: "The birthday bound, tail bounds, and the probabilistic method well enough to read a security proof: derive why collisions appear after roughly the square root of the space, and what a statistical distance is.",
          },
          {
            id: "maths-implement",
            text: "Implement, from the definitions, in Python: extended Euclid, modular inverse, the Chinese remainder theorem, square-and-multiply exponentiation, multiplication in the field of 256 elements used by AES, and polynomial multiplication modulo x to the n plus 1.",
          },
        ],
        gate: "The six routines from the last task pass a hundred random test vectors each, checked against Sage, written and passing in a single sitting of under two hours.",
        resources: [
          {
            title: "A Computational Introduction to Number Theory and Algebra",
            url: "https://shoup.net/ntb/",
            note: "Shoup. Free from the author. The one mathematics book for the whole plan; the chapters named in the tasks are the ones to do.",
          },
          {
            title: "SageMath",
            url: "https://www.sagemath.org/",
            note: "Install it now. Every algebraic object in the plan can be checked against it.",
          },
        ],
        practice: [
          {
            title: "CryptoHack, the mathematics track",
            url: "https://cryptohack.org/",
            note: "Modular arithmetic, lattices and elliptic curve challenges with a scoreboard. Log the challenges finished per week.",
          },
        ],
      },
      {
        id: "symmetric",
        title: "Symmetric cryptography and the idea of a proof",
        weeks: "Weeks 5 to 10",
        why: "Pseudorandom functions, block ciphers, message authentication and hashing are where security games and reductions are first learned, on objects small enough to hold in the head. The proofs here are the template for every proof later, lattice ones included.",
        tasks: [
          {
            id: "symmetric-definitions",
            text: "Katz and Lindell chapters 1 to 3: perfect secrecy, the one-time pad, the definition of computational security, and pseudorandom generators. Write the definitions out from memory at the end of each chapter.",
          },
          {
            id: "symmetric-prf",
            text: "Chapters 4 to 7: pseudorandom functions and permutations, modes of operation, message authentication codes, authenticated encryption and hash functions. Do the reduction exercises, not only the reading.",
          },
          {
            id: "symmetric-boneh",
            text: "Boneh and Shoup part one alongside, for the same material with more rigour and the game-hopping style used in current papers.",
          },
          {
            id: "symmetric-course",
            text: "Boneh's Cryptography I course, all weeks and all problem sets, as the graded check on the reading.",
          },
          {
            id: "symmetric-attacks",
            text: "Cryptopals sets 1 to 4: every byte-at-a-time and padding-oracle attack implemented yourself. These are the mistakes you will later be paid to find.",
          },
        ],
        gate: "Cryptopals sets one to four complete, and a written reduction proof that a cipher-block-chaining MAC on fixed-length messages is a secure MAC if the block cipher is a pseudorandom function, checked line by line against Katz and Lindell.",
        resources: [
          {
            title: "Introduction to Modern Cryptography",
            url: "https://www.cs.umd.edu/~jkatz/imc.html",
            note: "Katz and Lindell, third edition. The textbook for the classical stage; its proofs are the ones to imitate.",
          },
          {
            title: "A Graduate Course in Applied Cryptography",
            url: "https://toc.cryptobook.us/",
            note: "Boneh and Shoup. Free. Deeper than Katz and Lindell and closer to how papers are written now.",
          },
          {
            title: "Serious Cryptography",
            url: "https://nostarch.com/serious-cryptography-2nd-edition",
            note: "Aumasson, second edition. The practitioner's view of the same material, including what actually breaks in deployed systems.",
          },
        ],
        practice: [
          {
            title: "Cryptography I",
            url: "https://www.coursera.org/learn/crypto",
            note: "Boneh's course. The problem sets are graded; log the score on each.",
          },
          {
            title: "Cryptopals",
            url: "https://cryptopals.com/",
            note: "The challenge sets. Log the set and challenge number finished each week; the gate wants sets one to four.",
          },
        ],
      },
      {
        id: "publickey",
        title: "Public-key cryptography, the part that has to be replaced",
        weeks: "Weeks 11 to 16",
        why: "RSA, Diffie-Hellman and elliptic curves are exactly what a quantum computer removes, so this block learns them well enough to know what is being lost: the group structure, the assumptions, the constructions built on them, and the ways they fail when misused.",
        tasks: [
          {
            id: "publickey-numbers",
            text: "Katz and Lindell chapters 9 and 10: the algorithms on numbers and the assumptions, factoring, RSA, discrete logarithm, Diffie-Hellman, and the elliptic curve group.",
          },
          {
            id: "publickey-schemes",
            text: "Chapters 11 to 13: public-key encryption, hybrid encryption, padding and the random oracle model, digital signatures, and the Fiat-Shamir transform. Fiat-Shamir returns as the heart of ML-DSA.",
          },
          {
            id: "publickey-implement",
            text: "Implement RSA with OAEP, Diffie-Hellman over a safe prime, and ECDSA over the P-256 curve from the definitions, then verify each against the NIST validation program's test vectors.",
          },
          {
            id: "publickey-break",
            text: "Break each of your implementations through a known misuse: a small exponent with no padding, a reused ECDSA nonce, and an invalid-curve point. Recover the private key in every case.",
          },
          {
            id: "publickey-attacks",
            text: "Cryptopals sets 5 to 8: the Diffie-Hellman, RSA and elliptic curve attacks, implemented yourself.",
          },
        ],
        gate: "Cryptopals sets five to eight complete, and your own ECDSA over P-256 that passes the NIST vectors and recovers a private key from two signatures that reused a nonce, in under one hour from a cold start.",
        resources: [
          {
            title: "Introduction to Modern Cryptography",
            url: "https://www.cs.umd.edu/~jkatz/imc.html",
            note: "Katz and Lindell, chapters 9 to 13.",
          },
          {
            title: "Mathematics of Public Key Cryptography",
            url: "https://www.math.auckland.ac.nz/~sgal018/crypto-book/crypto-book.html",
            note: "Galbraith. Free from the author. The algebra behind discrete logarithms and elliptic curves in full, and later the lattice chapters.",
          },
          {
            title: "Cryptographic Algorithm Validation Program",
            url: "https://csrc.nist.gov/projects/cryptographic-algorithm-validation-program",
            note: "NIST's test vectors for every approved algorithm. An implementation that has not passed these is a sketch.",
          },
        ],
        practice: [
          {
            title: "Cryptopals",
            url: "https://cryptopals.com/",
            note: "Sets five to eight. Log the challenge number and the date each one fell.",
          },
          {
            title: "CryptoHack, the elliptic curve and RSA tracks",
            url: "https://cryptohack.org/",
            note: "Invalid curves, small subgroups and padding attacks with a scoreboard. Log the challenges finished.",
          },
        ],
      },
      {
        id: "protocols",
        title: "Protocols, implementations and formal tools",
        weeks: "Weeks 17 to 20",
        why: "Cryptography is deployed inside protocols, and most real failures are in the protocol or the implementation rather than the primitive. This block reads TLS 1.3 as a specification, learns constant-time discipline, and picks up the prover that finds protocol attacks mechanically.",
        tasks: [
          {
            id: "protocols-tls",
            text: "Read RFC 8446 end to end and draw the TLS 1.3 handshake with every key derived and what each one protects. Then read the Noise framework specification for the same ideas in a smaller space.",
          },
          {
            id: "protocols-engineering",
            text: "Real-World Cryptography, the chapters on key exchange, authenticated encryption, secure transport and hardware, for how the pieces are actually assembled and managed.",
          },
          {
            id: "protocols-constant-time",
            text: "Constant-time programming: read the BearSSL notes, then write a timing attack against a variable-time comparison in your own code and measure it working on your own machine.",
          },
          {
            id: "protocols-tamarin",
            text: "The Tamarin prover tutorial, then model a two-message authenticated key exchange, remove the authentication, and let the prover find the man in the middle.",
          },
        ],
        gate: "A Tamarin model of a two-message key exchange that finds the man-in-the-middle attack when authentication is removed and proves secrecy when it is restored, written and run in one weekend, with a page explaining what the prover checked.",
        resources: [
          {
            title:
              "RFC 8446, The Transport Layer Security Protocol Version 1.3",
            url: "https://www.rfc-editor.org/rfc/rfc8446",
            note: "Read as a specification: what is derived from what, and why each message is where it is.",
          },
          {
            title: "The Noise Protocol Framework",
            url: "https://noiseprotocol.org/noise.html",
            note: "The handshake patterns underneath WireGuard and Signal, small enough to read in an afternoon.",
          },
          {
            title: "Real-World Cryptography",
            url: "https://www.manning.com/books/real-world-cryptography",
            note: "Wong. The engineering chapters, and the last chapter is a first look at post-quantum schemes.",
          },
          {
            title: "BearSSL, why constant-time crypto",
            url: "https://www.bearssl.org/constanttime.html",
            note: "The clearest short account of what constant-time means and how to get it in C.",
          },
          {
            title: "Tamarin prover manual",
            url: "https://tamarin-prover.com/manual/",
            note: "The tutorial chapters, then the case studies. The prover returns in the last block for a post-quantum handshake.",
          },
        ],
        practice: [
          {
            title: "Tamarin prover",
            url: "https://tamarin-prover.com/",
            note: "The tool itself, with example models. Log which models you built and whether the prover terminated on each.",
          },
        ],
      },
    ],
  },
  {
    id: "quantum",
    title: "Quantum mechanics, for a cryptographer",
    weeks: "Weeks 21 to 36",
    why: "Enough quantum computing to derive Shor's algorithm and cost it, not to build a machine. Then quantum cryptography proper, quantum key distribution and its no-go theorems, because it is the other answer to the same threat and someone in a cryptography department will be asked to assess it.",
    exit: "You have a state-vector simulator of your own, Shor's algorithm running in it, a defended estimate of what a cryptographically relevant machine costs, and a two-page position on quantum key distribution against post-quantum cryptography that you would sign.",
    blocks: [
      {
        id: "qinfo",
        title: "Quantum information",
        weeks: "Weeks 21 to 26",
        why: "Qubits, unitaries, measurement, entanglement and the quantum Fourier transform, learned by building a simulator so that every claim about a circuit can be checked against numbers.",
        tasks: [
          {
            id: "qinfo-postulates",
            text: "Nielsen and Chuang chapters 1 and 2: state vectors, the postulates, tensor products, measurement in a basis, the Bloch sphere, and density matrices. Do the exercises.",
          },
          {
            id: "qinfo-country",
            text: "Quantum Country, all essays, with its spaced-repetition prompts kept up for the rest of the stage. It makes the notation automatic.",
          },
          {
            id: "qinfo-circuits",
            text: "Chapter 4 on quantum circuits and chapter 5 on the quantum Fourier transform and phase estimation. Draw the transform circuit for four qubits by hand and count its gates.",
          },
          {
            id: "qinfo-simulator",
            text: "Write a state-vector simulator in NumPy for up to ten qubits: single-qubit gates, controlled gates, measurement, and the Fourier transform built from them. Check it against Quirk circuit by circuit.",
          },
          {
            id: "qinfo-nocloning",
            text: "Prove the no-cloning theorem, derive the uncertainty relation for two conjugate bases, and show what a measurement in the wrong basis does to a state. These three are the whole foundation of the next block but one.",
          },
        ],
        gate: "A ten-qubit state-vector simulator whose amplitudes agree with Quirk to six decimal places on ten circuits including the quantum Fourier transform, and the four-qubit transform circuit derived on paper in under thirty minutes.",
        resources: [
          {
            title: "Quantum Computation and Quantum Information",
            url: "https://www.cambridge.org/highereducation/books/quantum-computation-and-quantum-information/01E10196D0A682A6AEFFEA52D53BE9AE",
            note: "Nielsen and Chuang. The standard text; chapters 1, 2, 4, 5 and later 12 are the ones the plan uses.",
          },
          {
            title: "Quantum Country",
            url: "https://quantum.country/",
            note: "Matuschak and Nielsen. Free. Quantum computing taught with spaced repetition built into the text.",
          },
          {
            title: "Introduction to Quantum Computing, lecture notes",
            url: "https://cs.uwaterloo.ca/~watrous/QC-notes/",
            note: "Watrous. Free. A mathematician's version of the same material, terse and exact.",
          },
        ],
        practice: [
          {
            title: "Quirk",
            url: "https://algassert.com/quirk",
            note: "A drag-and-drop circuit simulator that shows amplitudes live. Your simulator is scored against it; log the circuits matched.",
          },
          {
            title: "IBM Quantum Learning",
            url: "https://learning.quantum.ibm.com/",
            note: "The Qiskit courses with graded exercises. Log the modules finished.",
          },
        ],
      },
      {
        id: "shor",
        title: "Shor, Grover, and what they cost",
        weeks: "Weeks 27 to 32",
        why: "The threat, derived rather than quoted: period finding, the reduction from factoring and discrete logarithms to it, Grover's square root, and the resource estimates that turn an algorithm into a date. This is the block that lets you answer the question everyone asks.",
        tasks: [
          {
            id: "shor-period",
            text: "Derive Shor's algorithm from phase estimation: order finding, continued fractions, and the classical reduction from factoring to order finding. Nielsen and Chuang section 5.3 and appendix 4.",
          },
          {
            id: "shor-dlog",
            text: "Extend it to the discrete logarithm, then read the elliptic curve version. Understand why P-256 falls to fewer qubits than RSA-2048.",
          },
          {
            id: "shor-implement",
            text: "Run Shor's algorithm end to end in your own simulator to factor 15 and 21, with the continued fraction step written yourself.",
          },
          {
            id: "shor-grover",
            text: "Implement Grover's search on four qubits and plot the success probability against iterations; derive the optimal count and prove the square-root bound is tight.",
          },
          {
            id: "shor-estimates",
            text: "Read the 2019 and 2025 resource estimates for RSA-2048 and the 2017 one for elliptic curves. Write down the logical qubit count, the physical count under surface-code assumptions, and the runtime, and be able to say which assumption each number is most sensitive to.",
          },
        ],
        gate: "Shor's algorithm factoring 15 and 21 end to end in your own simulator, and a one-page estimate of the logical and physical qubits and the runtime for RSA-2048 with every assumption stated, defended against the numbers in the 2025 paper.",
        resources: [
          {
            title:
              "Polynomial-Time Algorithms for Prime Factorization and Discrete Logarithms on a Quantum Computer",
            url: "https://arxiv.org/abs/quant-ph/9508027",
            note: "Shor. The original, still the clearest statement of the reduction.",
          },
          {
            title:
              "How to factor 2048 bit RSA integers in 8 hours using 20 million noisy qubits",
            url: "https://arxiv.org/abs/1905.09749",
            note: "Gidney and Ekerå, 2019. The estimate every policy document quoted for five years.",
          },
          {
            title:
              "How to factor 2048 bit RSA integers with less than a million noisy qubits",
            url: "https://arxiv.org/abs/2505.15917",
            note: "Gidney, 2025. The twenty-fold reduction; read it for what changed and what did not.",
          },
          {
            title:
              "Quantum resource estimates for computing elliptic curve discrete logarithms",
            url: "https://arxiv.org/abs/1706.06752",
            note: "Roetteler, Naehrig, Svore and Lauter. Why elliptic curves fall first.",
          },
        ],
        practice: [
          {
            title: "IBM Quantum Learning",
            url: "https://learning.quantum.ibm.com/",
            note: "The Shor and Grover modules have exercises with answers; log the ones finished and the scores.",
          },
        ],
      },
      {
        id: "qkd",
        title: "Quantum cryptography and its limits",
        weeks: "Weeks 33 to 36",
        why: "Quantum key distribution is the other answer to the quantum threat, and it is sold hard. This block learns it well enough to derive a key rate, run the attacks on real implementations, and hold a position on where it belongs, which for most networks is nowhere near the core.",
        tasks: [
          {
            id: "qkd-protocols",
            text: "BB84, B92 and the entanglement-based protocol, from Nielsen and Chuang chapter 12 and the review paper. Compute the sifting rate and the error an intercept-and-resend attacker leaves, then simulate both.",
          },
          {
            id: "qkd-proof",
            text: "Read the Shor and Preskill proof and write out the argument in two pages: why entanglement purification implies BB84 is secure, and what the threshold error rate is.",
          },
          {
            id: "qkd-decoy",
            text: "The photon-number-splitting attack on weak coherent pulses, the decoy-state answer to it, and the GLLP key rate formula. Extend your simulation with both and compute the secret key rate against distance.",
          },
          {
            id: "qkd-beyond",
            text: "The no-go theorem for quantum bit commitment, device-independent key distribution, and quantum random number generation, from the Broadbent and Schaffner survey. Know what quantum mechanics cannot do for cryptography as well as what it can.",
          },
          {
            id: "qkd-position",
            text: "Read the NSA and NCSC positions on quantum key distribution and the ETSI standards work, then write a two-page memo on quantum key distribution against post-quantum cryptography for a federal network, with a recommendation.",
          },
        ],
        gate: "A BB84 simulation with the photon-number-splitting attack and decoy states whose secret key rate matches the GLLP formula within five percent at three distances, and the two-page position memo read and marked up by one colleague.",
        resources: [
          {
            title: "Advances in quantum cryptography",
            url: "https://arxiv.org/abs/1906.01645",
            note: "Pirandola and others, 2020. The review of the whole field; the sections on discrete-variable protocols and practical security are the ones to read closely.",
          },
          {
            title:
              "Simple proof of security of the BB84 quantum key distribution protocol",
            url: "https://arxiv.org/abs/quant-ph/0003004",
            note: "Shor and Preskill. Six pages. Read it three times.",
          },
          {
            title: "Decoy State Quantum Key Distribution",
            url: "https://arxiv.org/abs/quant-ph/0411004",
            note: "Lo, Ma and Chen. Why real systems with laser pulses can be secure at all.",
          },
          {
            title:
              "Security of quantum key distribution with imperfect devices",
            url: "https://arxiv.org/abs/quant-ph/0212066",
            note: "Gottesman, Lo, Lütkenhaus and Preskill. The key rate formula the gate asks for.",
          },
          {
            title: "Quantum cryptography beyond quantum key distribution",
            url: "https://arxiv.org/abs/1510.06120",
            note: "Broadbent and Schaffner. The no-go results and everything that is not key distribution.",
          },
          {
            title: "NSA on quantum key distribution and quantum cryptography",
            url: "https://www.nsa.gov/Cybersecurity/Quantum-Key-Distribution-QKD-and-Quantum-Cryptography-QC/",
            note: "The position a national security customer will hold. Read it before writing the memo.",
          },
          {
            title: "NCSC, quantum security technologies",
            url: "https://www.ncsc.gov.uk/whitepaper/quantum-security-technologies",
            note: "The UK view, which reaches the same conclusion by a different route.",
          },
          {
            title: "ETSI, quantum key distribution",
            url: "https://www.etsi.org/technologies/quantum-key-distribution",
            note: "Where the interface and security standards for the equipment are written.",
          },
        ],
        practice: [
          {
            title: "IBM Quantum Learning",
            url: "https://learning.quantum.ibm.com/",
            note: "The key distribution module runs BB84 on real hardware, with the error rate that implies. Log the rate you measured.",
          },
        ],
      },
    ],
  },
  {
    id: "postquantum",
    title: "Post-quantum cryptography, to expert",
    weeks: "Weeks 37 to 78",
    why: "The standards, the mathematics under them, the attacks that set their parameters, the other families kept as backups, and then the engineering of actually moving systems over. The second half of this stage is the job description; the first half is what makes the second half more than reading vendor sheets.",
    exit: "You have implemented ML-KEM and SLH-DSA from their FIPS documents and passed the validation vectors, you have reproduced the security estimates of the standard parameter sets with the lattice estimator, you have reproduced one published break, and you have written and defended a migration assessment for a real system and given a talk on it.",
    blocks: [
      {
        id: "lattices",
        title: "Lattices and the standards built on them",
        weeks: "Weeks 37 to 46",
        why: "ML-KEM, ML-DSA and FN-DSA are all one idea, learning with errors in a polynomial ring, with different constructions on top. This block learns the idea from the worst-case hardness results down to the byte encodings in the FIPS documents.",
        tasks: [
          {
            id: "lattices-lwe",
            text: "Regev's survey and Peikert's decade paper: lattices, the short integer solution and learning with errors problems, the worst-case to average-case reductions, and the ring and module variants. Derive why a decision LWE solver gives a search one.",
          },
          {
            id: "lattices-concepts",
            text: "Lyubashevsky's Basic Lattice Cryptography notes: the Kyber and Dilithium constructions explained from the assumptions up, including Fiat-Shamir with aborts and why the rejection step exists.",
          },
          {
            id: "lattices-ntt",
            text: "The number-theoretic transform in the ML-KEM ring: derive the roots of unity, implement the transform and its inverse, and show polynomial multiplication becomes pointwise. This is the arithmetic the whole family runs on.",
          },
          {
            id: "lattices-mlkem",
            text: "Implement ML-KEM-768 from FIPS 203 alone, with the reference code closed: key generation, encapsulation, decapsulation, the encodings and the Fujisaki-Okamoto transform. Pass the validation program's known-answer vectors.",
          },
          {
            id: "lattices-mldsa",
            text: "Read FIPS 204 and the draft FIPS 206 the same way and write out ML-DSA's signing loop with the reason for every rejection condition. Implementing it is optional; explaining it is not.",
          },
          {
            id: "lattices-proof",
            text: "Write the proof that the underlying public-key encryption in ML-KEM is secure against chosen-plaintext attack under module learning with errors, and derive the decryption failure probability from the noise distributions.",
          },
        ],
        gate: "An ML-KEM-768 implementation written from FIPS 203 alone that passes every known-answer vector in the NIST validation set, and the chosen-plaintext security reduction written out and checked by someone who has read the Kyber paper.",
        resources: [
          {
            title: "A Decade of Lattice Cryptography",
            url: "https://eprint.iacr.org/2015/939",
            note: "Peikert. Free. The survey of the field up to the standards; chapters 4 and 5 are the ones to work.",
          },
          {
            title: "The Learning with Errors Problem",
            url: "https://cims.nyu.edu/~regev/papers/lwesurvey.pdf",
            note: "Regev. Free. Twenty pages from the person who defined the problem.",
          },
          {
            title:
              "Basic Lattice Cryptography: the concepts behind Kyber (ML-KEM) and Dilithium (ML-DSA)",
            url: "https://eprint.iacr.org/2024/1287",
            note: "Lyubashevsky. Free. The designer's own explanation, at exactly the level the implementations need.",
          },
          {
            title:
              "FIPS 203, Module-Lattice-Based Key-Encapsulation Mechanism Standard",
            url: "https://csrc.nist.gov/pubs/fips/203/final",
            note: "The specification the gate is implemented from.",
          },
          {
            title: "FIPS 204, Module-Lattice-Based Digital Signature Standard",
            url: "https://csrc.nist.gov/pubs/fips/204/final",
          },
          {
            title: "CRYSTALS, Kyber and Dilithium",
            url: "https://pq-crystals.org/",
            note: "The design papers and the reference code, to be opened only after your own implementation passes.",
          },
        ],
        practice: [
          {
            title: "ACVP test vectors",
            url: "https://github.com/usnistgov/ACVP-Server",
            note: "NIST's known-answer vectors for ML-KEM, ML-DSA and SLH-DSA, under the generated JSON files. Log which parameter sets pass.",
          },
          {
            title: "CryptoHack, the lattice track",
            url: "https://cryptohack.org/",
            note: "Learning with errors and reduction challenges with a scoreboard. Log the challenges finished.",
          },
        ],
      },
      {
        id: "cryptanalysis",
        title: "Lattice cryptanalysis and why the parameters are what they are",
        weeks: "Weeks 47 to 52",
        why: "A parameter set is a bet on the best attack. This block learns lattice reduction well enough to run it, the estimator well enough to reproduce the security claims in the standards, and the hybrid and side-channel attacks that the estimator does not cover.",
        tasks: [
          {
            id: "cryptanalysis-lll",
            text: "Implement the LLL algorithm from Nguyen and Vallée's first chapter and use it to break a low-density knapsack cryptosystem and a small RSA instance with a partially known key.",
          },
          {
            id: "cryptanalysis-bkz",
            text: "Block Korkine-Zolotarev reduction, the Gaussian heuristic, and the core-SVP cost model. Derive why the cost is exponential in the block size and where the constant in the exponent comes from.",
          },
          {
            id: "cryptanalysis-attacks",
            text: "The primal and dual attacks on learning with errors, and the hybrid attack on small secrets. Work through how each one is costed in the estimator's source.",
          },
          {
            id: "cryptanalysis-estimator",
            text: "Run the lattice estimator on every ML-KEM and ML-DSA parameter set and reproduce the classical core-SVP numbers in the specifications. Write down which attack sets each one.",
          },
          {
            id: "cryptanalysis-sidechannels",
            text: "Read the KyberSlash timing attack and one fault attack on ML-DSA, then find the division in a naive ML-KEM implementation and remove it.",
          },
        ],
        gate: "Your own LLL that recovers the secret of a forty-dimensional low-density knapsack, and an estimator run for every ML-KEM parameter set that reproduces the classical core-SVP security levels in FIPS 203 within two bits, with the setting attack named for each.",
        resources: [
          {
            title: "The LLL Algorithm: Survey and Applications",
            url: "https://link.springer.com/book/10.1007/978-3-642-02295-1",
            note: "Nguyen and Vallée. The reference on lattice reduction; the first three chapters are what the block needs.",
          },
          {
            title: "Lattice-based Cryptography",
            url: "https://cims.nyu.edu/~regev/papers/pqc.pdf",
            note: "Micciancio and Regev. Free. The chapter from the 2009 book, still the best short account of the attacks.",
          },
          {
            title: "Estimate all the LWE and NTRU schemes",
            url: "https://estimate-all-the-lwe-ntru-schemes.github.io/docs/",
            note: "Albrecht and others. How the competition's candidates were costed against each other.",
          },
          {
            title: "KyberSlash",
            url: "https://kyberslash.cr.yp.to/",
            note: "The timing attack from a compiler-emitted division. The clearest example of why constant time is not a property of source code.",
          },
        ],
        practice: [
          {
            title: "The lattice estimator",
            url: "https://github.com/malb/lattice-estimator",
            note: "Albrecht's Sage tool. Log the parameter sets estimated and the bits the estimate gives for each.",
          },
        ],
      },
      {
        id: "families",
        title: "Hash-based, code-based, multivariate and isogeny schemes",
        weeks: "Weeks 53 to 60",
        why: "The backups, kept because lattices might fall. Hash-based signatures rest on the least assumption there is and are already required for firmware signing; code-based encryption is the oldest post-quantum scheme and now a standard; multivariate and isogeny schemes are where the most instructive breaks happened.",
        tasks: [
          {
            id: "families-hash",
            text: "Lamport, Winternitz, Merkle trees, and the stateful schemes in SP 800-208; then the hypertree and few-time signatures that make SLH-DSA stateless. Understand exactly what goes wrong if a stateful key is used twice.",
          },
          {
            id: "families-slhdsa",
            text: "Implement SLH-DSA-SHAKE-128s from FIPS 205 alone and pass the known-answer vectors.",
          },
          {
            id: "families-codes",
            text: "Goppa codes, the McEliece and Niederreiter systems, and why information-set decoding sets their parameters. Then HQC, the scheme chosen as the backup to ML-KEM, and how it differs.",
          },
          {
            id: "families-multivariate",
            text: "Oil and vinegar signatures and the MAYO candidate, then reproduce Beullens's break of Rainbow on a toy parameter set in Sage.",
          },
          {
            id: "families-isogeny",
            text: "Supersingular isogenies at the level of the Castryck and Decru break: what SIKE assumed, what the attack used, and why SQIsign survives it. Run the published break script and explain each step.",
          },
          {
            id: "families-compare",
            text: "Write a comparison of the four families for a firmware signing use case: key and signature sizes, signing and verification speed, the state problem, and what a break in each would look like.",
          },
        ],
        gate: "SLH-DSA-SHAKE-128s written from FIPS 205 alone and passing every known-answer vector, and the four-family comparison for firmware signing as a one-page table with sizes and speeds measured from your own or reference code.",
        resources: [
          {
            title: "FIPS 205, Stateless Hash-Based Digital Signature Standard",
            url: "https://csrc.nist.gov/pubs/fips/205/final",
            note: "The specification the gate is implemented from.",
          },
          {
            title:
              "SP 800-208, Recommendation for Stateful Hash-Based Signature Schemes",
            url: "https://csrc.nist.gov/pubs/sp/800/208/final",
            note: "LMS and XMSS as national security systems already use them.",
          },
          {
            title: "SPHINCS+",
            url: "https://sphincs.org/",
            note: "The design papers behind SLH-DSA; open after your own implementation passes.",
          },
          {
            title: "Classic McEliece",
            url: "https://classic.mceliece.org/",
            note: "The conservative code-based system and the arguments for it.",
          },
          {
            title: "HQC",
            url: "https://pqc-hqc.org/",
            note: "The code-based scheme NIST chose in 2025 as a second key encapsulation mechanism.",
          },
          {
            title: "Breaking Rainbow takes a weekend on a laptop",
            url: "https://eprint.iacr.org/2022/214",
            note: "Beullens. A finalist removed by one paper; the block reproduces it.",
          },
          {
            title: "An efficient key recovery attack on SIDH",
            url: "https://eprint.iacr.org/2022/975",
            note: "Castryck and Decru. The other finalist removed by one paper.",
          },
          {
            title: "MAYO",
            url: "https://pqmayo.org/",
            note: "The multivariate candidate still standing in the additional signature process.",
          },
          {
            title: "SQIsign",
            url: "https://sqisign.org/",
            note: "The isogeny signature that survived, and why.",
          },
          {
            title: "Post-Quantum Cryptography",
            url: "https://link.springer.com/book/10.1007/978-3-540-88702-7",
            note: "Bernstein, Buchmann and Dahmen. Dated but still the one book that introduces every family in one place.",
          },
        ],
        practice: [
          {
            title: "ACVP test vectors",
            url: "https://github.com/usnistgov/ACVP-Server",
            note: "The SLH-DSA vectors for the gate. Log the parameter sets that pass.",
          },
          {
            title: "NIST additional digital signature schemes",
            url: "https://csrc.nist.gov/projects/pqc-dig-sig",
            note: "The round three candidates and their submissions. Log one candidate read in full per fortnight and a paragraph on its assumption.",
          },
        ],
      },
      {
        id: "migration",
        title: "Migration engineering",
        weeks: "Weeks 61 to 68",
        why: "The job. Hybrid key exchange in real protocols, certificates and signatures, hardware modules and validation, inventory and prioritisation, and the government timelines the work is measured against. The gate is a migration assessment someone else has to read.",
        tasks: [
          {
            id: "migration-hybrid",
            text: "Read the hybrid key exchange draft for TLS 1.3, then stand up a server and client with a post-quantum capable OpenSSL, capture the handshake, and decode the key share field by field.",
          },
          {
            id: "migration-protocols",
            text: "Post-quantum in SSH, IKEv2 and Signal: read how each one composed the classical and post-quantum halves and what each was worried about. Note where they disagree.",
          },
          {
            id: "migration-certificates",
            text: "ML-DSA in X.509 and the composite signature drafts from the IETF LAMPS working group, and the size problem they create in certificate chains and TLS handshakes.",
          },
          {
            id: "migration-hardware",
            text: "Hardware security modules and FIPS 140-3 validation for the new algorithms: what a validated module can and cannot do today, and how the validation program tests an implementation.",
          },
          {
            id: "migration-timelines",
            text: "NIST IR 8547, CNSA 2.0 and the CISA guidance, as a table: which system class moves by which date. Then the Post-Quantum Cryptography Coalition's migration roadmap for the practical sequence.",
          },
          {
            id: "migration-assessment",
            text: "Pick a real system you can see all of and write a migration assessment: a cryptographic inventory, a ranking by exposure to harvest-now-decrypt-later, a timeline against the standards, and the crypto-agility changes needed first.",
          },
        ],
        gate: "A hybrid TLS 1.3 handshake captured and decoded field by field with the key share sizes annotated, and a migration assessment for one real system with an inventory, a risk ranking and a timeline, read and marked up by two people who work on that system.",
        resources: [
          {
            title: "Post-quantum hybrid ECDHE-MLKEM key agreement for TLS 1.3",
            url: "https://datatracker.ietf.org/doc/draft-ietf-tls-ecdhe-mlkem/",
            note: "The draft behind the key exchange most of the internet now negotiates.",
          },
          {
            title: "Open Quantum Safe",
            url: "https://openquantumsafe.org/",
            note: "liboqs and the OpenSSL provider, for standing up post-quantum TLS and SSH on a laptop.",
          },
          {
            title: "PQClean",
            url: "https://github.com/PQClean/PQClean",
            note: "Clean, portable, tested implementations of the standards, and the place to contribute in the last block.",
          },
          {
            title: "Composite ML-DSA signatures for X.509",
            url: "https://datatracker.ietf.org/doc/draft-ietf-lamps-pq-composite-sigs/",
            note: "The LAMPS working group's answer to certificates during the transition.",
          },
          {
            title:
              "NIST IR 8547, Transition to Post-Quantum Cryptography Standards",
            url: "https://csrc.nist.gov/pubs/ir/8547/ipd",
            note: "The federal timeline: deprecation in 2030, disallowed in 2035.",
          },
          {
            title: "Commercial National Security Algorithm Suite 2.0",
            url: "https://media.defense.gov/2022/Sep/07/2003071834/-1/-1/0/CSA_CNSA_2.0_ALGORITHMS_.PDF",
            note: "The NSA's suite and dates for national security systems.",
          },
          {
            title: "CISA, post-quantum cryptography",
            url: "https://www.cisa.gov/quantum",
            note: "The civilian agency guidance and the inventory templates.",
          },
          {
            title: "Post-Quantum Cryptography Coalition",
            url: "https://pqcc.org/",
            note: "The migration roadmap and the working groups doing the practical sequencing.",
          },
        ],
        practice: [
          {
            title: "Open Quantum Safe",
            url: "https://openquantumsafe.org/",
            note: "The demos and interoperability tests. Log which protocols you stood up and which key exchange each negotiated.",
          },
        ],
      },
      {
        id: "research",
        title: "The research edge and the community",
        weeks: "Weeks 69 to 78, then ongoing",
        why: "Expert means the field comes to you: you read the new results as they appear, you can reproduce them, you contribute code the field uses, and you can stand in front of the department and explain a break the week it happens.",
        tasks: [
          {
            id: "research-reading",
            text: "Read the post-quantum forum and the ePrint listing daily and keep a log of every result that would change a parameter set, a standard, or a deployment recommendation, with one paragraph each.",
          },
          {
            id: "research-reproduce",
            text: "Reproduce one implementation or attack paper from the last two years end to end, with a public repository and a write-up of what did and did not reproduce.",
          },
          {
            id: "research-contribute",
            text: "Land one pull request in PQClean, liboqs or a comparable library: a test, a constant-time fix, a new parameter set, or a port.",
          },
          {
            id: "research-verify",
            text: "Formal verification of an implementation: work through a verified ML-KEM component in hax or EasyCrypt far enough to explain what the proof covers and what it leaves out.",
          },
          {
            id: "research-talk",
            text: "Give a talk to the department on one recent result, with the mathematics derived on the board and the consequence for a deployment stated.",
          },
          {
            id: "research-conference",
            text: "Attend Real World Crypto or the PQCrypto conference, or follow the NIST standardisation conference recordings, and write up three talks that change what you would recommend.",
          },
        ],
        gate: "One reproduced paper with a public repository and write-up, one merged pull request to a post-quantum library, and one talk given to the department, all three inside the same six months.",
        resources: [
          {
            title: "Cryptology ePrint Archive",
            url: "https://eprint.iacr.org/",
            note: "Where results appear first. The daily listing is the reading habit.",
          },
          {
            title: "NIST pqc-forum",
            url: "https://groups.google.com/a/list.nist.gov/g/pqc-forum",
            note: "The mailing list where the standards are argued about in public.",
          },
          {
            title: "Real World Crypto",
            url: "https://rwc.iacr.org/",
            note: "The conference closest to deployment; the talks are recorded.",
          },
          {
            title: "PQCrypto",
            url: "https://pqcrypto.org/",
            note: "The conference series for the field itself.",
          },
          {
            title: "hax",
            url: "https://hax.cryspen.com/",
            note: "The Rust-to-proof toolchain used for the verified ML-KEM in libcrux.",
          },
          {
            title: "EasyCrypt",
            url: "https://www.easycrypt.info/",
            note: "The proof assistant behind the Formosa verified implementations.",
          },
        ],
        practice: [
          {
            title: "PQClean",
            url: "https://github.com/PQClean/PQClean",
            note: "Contributions are the score. Log each pull request opened and its state.",
          },
        ],
      },
    ],
  },
];

/** The blocks in page order, for the tests and the progress count. */
export const plan: Block[] = stages.flatMap((stage) => stage.blocks);

export const allTasks = tasksOf(plan);

export const bookshelf: Book[] = [
  {
    title: "A Computational Introduction to Number Theory and Algebra",
    authors: "Victor Shoup",
    edition: "second edition",
    level: "classical",
    url: "https://shoup.net/ntb/",
    free: true,
    why: "All the algebra the plan needs, proved, with exercises, from the author of half the reductions in the field.",
  },
  {
    title: "Introduction to Modern Cryptography",
    authors: "Jonathan Katz and Yehuda Lindell",
    edition: "third edition",
    level: "classical",
    url: "https://www.cs.umd.edu/~jkatz/imc.html",
    free: false,
    why: "The standard textbook. Its definitions and proofs are the template for reading everything after it.",
  },
  {
    title: "A Graduate Course in Applied Cryptography",
    authors: "Dan Boneh and Victor Shoup",
    level: "classical",
    url: "https://toc.cryptobook.us/",
    free: true,
    why: "Deeper than Katz and Lindell and written in the game-hopping style current papers use. The free one for the stage.",
  },
  {
    title: "Serious Cryptography",
    authors: "Jean-Philippe Aumasson",
    edition: "second edition",
    level: "classical",
    url: "https://nostarch.com/serious-cryptography-2nd-edition",
    free: false,
    why: "What breaks in practice, from someone who breaks it. Short enough to read alongside the textbook.",
  },
  {
    title: "Real-World Cryptography",
    authors: "David Wong",
    level: "classical",
    url: "https://www.manning.com/books/real-world-cryptography",
    free: false,
    why: "How the primitives are assembled into systems, with a first chapter on post-quantum schemes at the end.",
  },
  {
    title: "Mathematics of Public Key Cryptography",
    authors: "Steven Galbraith",
    level: "classical",
    url: "https://www.math.auckland.ac.nz/~sgal018/crypto-book/crypto-book.html",
    free: true,
    why: "Discrete logarithms, elliptic curves and lattices at full mathematical depth, free from the author.",
  },
  {
    title: "Quantum Computation and Quantum Information",
    authors: "Michael Nielsen and Isaac Chuang",
    edition: "tenth anniversary edition",
    level: "quantum",
    url: "https://www.cambridge.org/highereducation/books/quantum-computation-and-quantum-information/01E10196D0A682A6AEFFEA52D53BE9AE",
    free: false,
    why: "The standard text, and its chapter 12 is the only textbook treatment of quantum cryptography most people will need.",
  },
  {
    title: "Quantum Country",
    authors: "Andy Matuschak and Michael Nielsen",
    level: "quantum",
    url: "https://quantum.country/",
    free: true,
    why: "The notation made automatic by spaced repetition. Read it before Nielsen and Chuang, not instead.",
  },
  {
    title: "Introduction to Quantum Computing, lecture notes",
    authors: "John Watrous",
    level: "quantum",
    url: "https://cs.uwaterloo.ca/~watrous/QC-notes/",
    free: true,
    why: "The same material as a mathematician writes it, for when the textbook is being too gentle.",
  },
  {
    title: "Advances in quantum cryptography",
    authors: "Stefano Pirandola and others",
    level: "quantum",
    url: "https://arxiv.org/abs/1906.01645",
    free: true,
    why: "A review paper rather than a book, but it is the book on quantum key distribution as deployed, and it is free.",
  },
  {
    title: "A Decade of Lattice Cryptography",
    authors: "Chris Peikert",
    level: "postquantum",
    url: "https://eprint.iacr.org/2015/939",
    free: true,
    why: "The survey that the lattice standards grew out of, from one of the people who grew them.",
  },
  {
    title:
      "Basic Lattice Cryptography: the concepts behind Kyber (ML-KEM) and Dilithium (ML-DSA)",
    authors: "Vadim Lyubashevsky",
    level: "postquantum",
    url: "https://eprint.iacr.org/2024/1287",
    free: true,
    why: "The designer explaining the designs at exactly the level an implementer needs.",
  },
  {
    title: "The LLL Algorithm: Survey and Applications",
    authors: "Phong Nguyen and Brigitte Vallée, editors",
    level: "postquantum",
    url: "https://link.springer.com/book/10.1007/978-3-642-02295-1",
    free: false,
    why: "Lattice reduction from the people who understand it best. The first three chapters are the cryptanalysis block.",
  },
  {
    title: "Post-Quantum Cryptography",
    authors: "Daniel Bernstein, Johannes Buchmann and Erik Dahmen, editors",
    level: "postquantum",
    url: "https://link.springer.com/book/10.1007/978-3-540-88702-7",
    free: false,
    why: "From 2009, so the parameters are stale, but still the one book that introduces every family side by side.",
  },
  {
    title: "FIPS 203, 204 and 205",
    authors: "National Institute of Standards and Technology",
    level: "postquantum",
    url: "https://csrc.nist.gov/pubs/fips/203/final",
    free: true,
    why: "The standards themselves. Two of the gates are implemented from these with everything else closed.",
  },
];
