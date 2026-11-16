//! Checking a Lean proof on Compiler Explorer.
//!
//! `POST /prove` takes the owner's Lean file and the statement it must
//! prove. The grader wraps the file, sends it to Compiler Explorer's Lean
//! (the [`LEAN_COMPILER`] pin), and reads Lean's own report. The proof
//! passes only when all four hold:
//!
//! 1. Lean compiled the file with no error.
//! 2. The file defines `answer`, and its type is the statement's, compared
//!    fully elaborated (`pp.all`). Notation, an instance, or a local
//!    definition that changes what the statement means changes that text.
//! 3. `answer` depends on no axiom outside [`ALLOWED_AXIOMS`]. `sorry` is
//!    the axiom `sorryAx`, and `native_decide` is `Lean.ofReduceBool`.
//! 4. The file uses none of the [`FORBIDDEN`] commands, which run code in
//!    Lean's elaborator and could rewrite what it reports.
//!
//! The statement is elaborated first, in a clean file, as a theorem named
//! with a value the owner cannot know. Lean's report after the owner's file
//! is fenced by markers that carry the same value, so text the file prints
//! cannot pass for the report. A file that hides the checks, such as one
//! that leaves a comment open, leaves no fenced report and fails.

use std::collections::hash_map::RandomState;
use std::hash::{BuildHasher, Hasher};

use serde::{Deserialize, Serialize};

use crate::request::{MAX_RETURNED_BYTES, MAX_SOURCE_BYTES, cut};
use crate::upstream::{CompilerExplorer, Execution, UpstreamError, joined};

/// Lean 4.29.1 on Compiler Explorer, from /api/compilers/lean on 16 Nov 2026.
/// It is core Lean, with no Mathlib.
pub const LEAN_COMPILER: &str = "lean_4_29_1";

/// The most bytes in a statement.
pub const MAX_STATEMENT_BYTES: usize = 1024;

/// The axioms a proof may rest on: Lean's three standard axioms.
pub const ALLOWED_AXIOMS: [&str; 3] = ["propext", "Classical.choice", "Quot.sound"];

/// Words that open metaprogramming, change the kernel's checks, or run
/// code while Lean elaborates. A proof has no use for them.
pub const FORBIDDEN: [&str; 14] = [
    "#eval",
    "#exit",
    "elab",
    "macro",
    "syntax",
    "run_cmd",
    "run_elab",
    "run_meta",
    "initialize",
    "builtin_initialize",
    "unsafe",
    "implemented_by",
    "extern",
    "set_option",
];

/// POST /prove.
#[derive(Debug, Clone, Deserialize, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct ProveRequest {
    /// The owner's Lean file. It defines `theorem answer`.
    pub source: String,
    /// The proposition `answer` must prove, in Lean, on one line.
    pub statement: String,
}

/// What the check found.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum Outcome {
    /// Every check held.
    Proved,
    /// Lean reported an error.
    Error,
    /// The proof uses `sorry`.
    Sorry,
    /// The proof rests on an axiom outside [`ALLOWED_AXIOMS`].
    Axiom,
    /// `answer` proves something other than the statement, or is missing.
    Statement,
    /// The file uses a word in [`FORBIDDEN`].
    Forbidden,
}

/// The answer to POST /prove.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ProveResponse {
    /// What the check found.
    pub outcome: Outcome,
    /// Why it failed, in a sentence, or `None` when it passed.
    pub message: Option<String>,
    /// Lean's messages about the owner's file, cut to a size.
    pub output: String,
}

impl ProveRequest {
    /// Why the grader refuses this request, or `None` when it can check it.
    #[must_use]
    pub fn problem(&self) -> Option<String> {
        if self.source.trim().is_empty() {
            return Some("The proof is empty.".into());
        }
        if self.source.len() > MAX_SOURCE_BYTES {
            return Some(format!(
                "The proof is at most {} KiB.",
                MAX_SOURCE_BYTES / 1024
            ));
        }
        if self.statement.trim().is_empty() {
            return Some("The statement is empty.".into());
        }
        if self.statement.len() > MAX_STATEMENT_BYTES {
            return Some(format!(
                "The statement is at most {MAX_STATEMENT_BYTES} bytes."
            ));
        }
        if self.statement.contains(['\n', '\r']) || self.statement.contains(":=") {
            return Some("The statement is one line with no `:=`.".into());
        }
        None
    }
}

/// The first word of [`FORBIDDEN`] that `source` uses as a whole word.
#[must_use]
pub fn forbidden_word(source: &str) -> Option<&'static str> {
    FORBIDDEN
        .iter()
        .copied()
        .find(|word| contains_word(source, word))
}

/// Whether `word` occurs in `text` with no identifier character on either side.
#[must_use]
pub fn contains_word(text: &str, word: &str) -> bool {
    let is_ident = |c: char| c.is_alphanumeric() || c == '_' || c == '.' || c == '\'';
    text.match_indices(word).any(|(at, _)| {
        let before = text[..at].chars().next_back();
        let after = text[at + word.len()..].chars().next();
        !before.is_some_and(is_ident) && !after.is_some_and(is_ident)
    })
}

/// A value the owner's file cannot know, as 32 hex digits.
#[must_use]
pub fn fence_value() -> String {
    let half = || {
        let mut hasher = RandomState::new().build_hasher();
        hasher.write_u8(0);
        hasher.finish()
    };
    format!("{:016x}{:016x}", half(), half())
}

/// The file sent to Lean: the statement alone, the owner's file, then the
/// fenced checks.
#[must_use]
pub fn wrapped(request: &ProveRequest, marker: &str) -> String {
    let reference = format!("reference_{marker}");
    let fence = |part: &str| format!("#print \"{marker}:{part}\"\n");
    format!(
        "theorem {reference} : {statement} := sorry\n\
         {source}\n\
         /-! checks -/\n\
         {statement_fence}\
         set_option pp.all true in\n#check @{reference}\n\
         {answer_fence}\
         set_option pp.all true in\n#check @answer\n\
         {axioms_fence}\
         #print axioms answer\n\
         {end_fence}",
        statement = request.statement,
        source = request.source,
        statement_fence = fence("statement"),
        answer_fence = fence("answer"),
        axioms_fence = fence("axioms"),
        end_fence = fence("end"),
    )
}

/// The text between the fence line `{marker}:{from}` and the fence line
/// `{marker}:{to}`, or `None` when either is missing or out of order.
#[must_use]
pub fn between<'a>(output: &'a str, marker: &str, from: &str, to: &str) -> Option<&'a str> {
    fenced(
        output,
        &format!("{marker}:{from}"),
        &format!("{marker}:{to}"),
    )
}

/// The text after the last `open` and before the first `close` that
/// follows it. A plain byte search, so Kani can check it for every input.
#[must_use]
pub fn fenced<'a>(output: &'a str, open: &str, close: &str) -> Option<&'a str> {
    let bytes = output.as_bytes();
    let (open, close) = (open.as_bytes(), close.as_bytes());
    if open.is_empty() || close.is_empty() {
        return None;
    }
    let at = |i: usize, needle: &[u8]| bytes.get(i..i + needle.len()) == Some(needle);
    let start = (0..bytes.len()).rev().find(|&i| at(i, open))? + open.len();
    let end = (start..bytes.len()).find(|&i| at(i, close))?;
    output.get(start..end)
}

/// The type in a `#check` line: everything after `name : `, with whitespace
/// runs made one space.
#[must_use]
pub fn checked_type(report: &str, name: &str) -> Option<String> {
    let prefix = format!("{name} :");
    let rest = report.trim().strip_prefix(&prefix)?;
    Some(rest.split_whitespace().collect::<Vec<_>>().join(" "))
}

/// The axioms in a `#print axioms` report, or `None` when it is not one.
#[must_use]
pub fn axioms(report: &str) -> Option<Vec<String>> {
    let text = report.split_whitespace().collect::<Vec<_>>().join(" ");
    if text == "'answer' does not depend on any axioms" {
        return Some(Vec::new());
    }
    let list = text
        .strip_prefix("'answer' depends on axioms: [")?
        .strip_suffix(']')?;
    Some(
        list.split(',')
            .map(|name| name.trim().to_owned())
            .filter(|name| !name.is_empty())
            .collect(),
    )
}

/// Lean's messages about the owner's file: everything before the first
/// fence, without the messages about the statement line the grader put
/// first, and with line numbers counted in the owner's file.
#[must_use]
pub fn owner_output(output: &str, marker: &str) -> String {
    let fence = format!("{marker}:statement");
    let text = output.find(&fence).map_or(output, |at| &output[..at]);
    let mut kept = Vec::new();
    let mut skipping = false;
    for line in text.lines() {
        if let Some(rest) = line.strip_prefix("<source>:") {
            let (number, tail) = rest.split_once(':').unwrap_or((rest, ""));
            match number.parse::<u64>() {
                Ok(1) => {
                    skipping = true;
                    continue;
                }
                Ok(n) => {
                    skipping = false;
                    kept.push(format!("<source>:{}:{tail}", n - 1));
                    continue;
                }
                Err(_) => skipping = false,
            }
        }
        if !skipping {
            kept.push(line.to_owned());
        }
    }
    cut(kept.join("\n").trim(), MAX_RETURNED_BYTES)
}

/// Read Lean's report on the wrapped file.
#[must_use]
pub fn judge(run: &Execution, marker: &str) -> ProveResponse {
    let output = joined(&run.stdout) + "\n" + &joined(&run.stderr);
    let reply = |outcome, message: &str| ProveResponse {
        outcome,
        message: Some(message.into()),
        output: owner_output(&output, marker),
    };
    if run.code != 0 {
        return reply(Outcome::Error, "Lean reported an error.");
    }
    let (Some(statement), Some(answer), Some(report)) = (
        between(&output, marker, "statement", "answer"),
        between(&output, marker, "answer", "axioms"),
        between(&output, marker, "axioms", "end"),
    ) else {
        return reply(Outcome::Error, "Lean's report on the proof was missing.");
    };
    let reference = format!("reference_{marker}");
    match (
        checked_type(statement, &reference),
        checked_type(answer, "answer"),
    ) {
        (Some(wanted), Some(got)) if wanted == got => {}
        _ => {
            return reply(
                Outcome::Statement,
                "`answer` does not prove the statement as written.",
            );
        }
    }
    let Some(used) = axioms(report) else {
        return reply(Outcome::Error, "Lean's report on axioms was unreadable.");
    };
    if used.iter().any(|name| name == "sorryAx") {
        return reply(Outcome::Sorry, "The proof uses `sorry`.");
    }
    if let Some(name) = used
        .iter()
        .find(|name| !ALLOWED_AXIOMS.contains(&name.as_str()))
    {
        return reply(
            Outcome::Axiom,
            &format!("The proof rests on the axiom `{name}`."),
        );
    }
    ProveResponse {
        outcome: Outcome::Proved,
        message: None,
        output: owner_output(&output, marker),
    }
}

/// Check `request` on Compiler Explorer's Lean.
///
/// # Errors
///
/// When Compiler Explorer gives no answer to read.
pub async fn prove(
    explorer: &CompilerExplorer,
    request: &ProveRequest,
) -> Result<ProveResponse, UpstreamError> {
    if let Some(word) = forbidden_word(&request.source) {
        return Ok(ProveResponse {
            outcome: Outcome::Forbidden,
            message: Some(format!("A proof here cannot use `{word}`.")),
            output: String::new(),
        });
    }
    let marker = fence_value();
    let run = explorer
        .compile(LEAN_COMPILER, "lean", &wrapped(request, &marker))
        .await?;
    Ok(judge(&run, &marker))
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::upstream::Line;

    const FENCE: &str = "0123456789abcdef0123456789abcdef";

    fn lines(text: &str) -> Vec<Line> {
        text.lines()
            .map(|line| Line { text: line.into() })
            .collect()
    }

    /// Lean's report for `theorem answer : ∀ n : Nat, n + 0 = n`, from
    /// `lean_4_29_1` on 16 Nov 2026, with the given axioms line.
    fn report(answer_type: &str, axioms_line: &str) -> String {
        let wanted = "∀ (n : Nat),\n  @Eq.{1} Nat\n    (@HAdd.hAdd.{0, 0, 0} Nat Nat Nat (@instHAdd.{0} Nat instAddNat) n\n      (@OfNat.ofNat.{0} Nat (nat_lit 0) (instOfNatNat (nat_lit 0))))\n    n";
        format!(
            "<source>:1:8: warning: declaration uses `sorry`\n{FENCE}:statement\nreference_{FENCE} : {wanted}\n{FENCE}:answer\nanswer : {answer_type}\n{FENCE}:axioms\n{axioms_line}\n{FENCE}:end"
        )
    }

    const SAME: &str = "∀ (n : Nat),\n  @Eq.{1} Nat\n    (@HAdd.hAdd.{0, 0, 0} Nat Nat Nat (@instHAdd.{0} Nat instAddNat) n\n      (@OfNat.ofNat.{0} Nat (nat_lit 0) (instOfNatNat (nat_lit 0))))\n    n";

    fn run(code: i64, stdout: &str) -> Execution {
        Execution {
            code,
            stdout: lines(stdout),
            ..Execution::default()
        }
    }

    #[test]
    fn a_complete_proof_is_proved() {
        let answer = judge(
            &run(0, &report(SAME, "'answer' does not depend on any axioms")),
            FENCE,
        );
        assert_eq!(answer.outcome, Outcome::Proved);
        assert_eq!(answer.message, None);
        assert_eq!(answer.output, "");
    }

    #[test]
    fn the_standard_axioms_are_allowed() {
        let line = "'answer' depends on axioms: [propext, Classical.choice, Quot.sound]";
        assert_eq!(
            judge(&run(0, &report(SAME, line)), FENCE).outcome,
            Outcome::Proved
        );
    }

    #[test]
    fn sorry_fails() {
        let line = "'answer' depends on axioms: [sorryAx]";
        assert_eq!(
            judge(&run(0, &report(SAME, line)), FENCE).outcome,
            Outcome::Sorry
        );
    }

    #[test]
    fn native_decide_and_new_axioms_fail() {
        let line = "'answer' depends on axioms: [propext, Lean.ofReduceBool]";
        let answer = judge(&run(0, &report(SAME, line)), FENCE);
        assert_eq!(answer.outcome, Outcome::Axiom);
        assert_eq!(
            answer.message.as_deref(),
            Some("The proof rests on the axiom `Lean.ofReduceBool`.")
        );
    }

    #[test]
    fn a_changed_statement_fails() {
        let other = SAME.replace("instAddNat", "instAddNat_example");
        let line = "'answer' does not depend on any axioms";
        assert_eq!(
            judge(&run(0, &report(&other, line)), FENCE).outcome,
            Outcome::Statement
        );
    }

    #[test]
    fn an_error_fails_before_anything_is_read() {
        let line = "'answer' does not depend on any axioms";
        assert_eq!(
            judge(&run(1, &report(SAME, line)), FENCE).outcome,
            Outcome::Error
        );
    }

    #[test]
    fn a_report_without_fences_fails() {
        let answer = judge(
            &run(0, "answer : True\n'answer' does not depend on any axioms"),
            FENCE,
        );
        assert_eq!(answer.outcome, Outcome::Error);
    }

    #[test]
    fn fences_with_another_value_do_not_count() {
        let forged = report(SAME, "'answer' does not depend on any axioms")
            .replace(FENCE, "ffffffffffffffffffffffffffffffff");
        assert_eq!(judge(&run(0, &forged), FENCE).outcome, Outcome::Error);
    }

    #[test]
    fn forbidden_words_are_found_as_whole_words() {
        assert_eq!(forbidden_word("#eval 1"), Some("#eval"));
        assert_eq!(forbidden_word("macro_rules | `(x) => `(y)"), None);
        assert_eq!(
            forbidden_word("macro \"t\" : tactic => `(tactic| rfl)"),
            Some("macro")
        );
        assert_eq!(
            forbidden_word("set_option maxHeartbeats 0 in"),
            Some("set_option")
        );
        assert_eq!(forbidden_word("theorem answer : True := trivial"), None);
        assert_eq!(forbidden_word("theorem elaborate : True := trivial"), None);
        assert_eq!(forbidden_word("theorem my.elab : True := trivial"), None);
    }

    #[test]
    fn the_wrapped_file_puts_the_statement_first_and_the_checks_last() {
        let request = ProveRequest {
            source: "theorem answer : True := trivial".into(),
            statement: "True".into(),
        };
        let file = wrapped(&request, FENCE);
        assert!(file.starts_with(&format!("theorem reference_{FENCE} : True := sorry\n")));
        assert!(file.ends_with(&format!("#print \"{FENCE}:end\"\n")));
        let source_at = file.find("theorem answer").unwrap_or(usize::MAX);
        let first_check = file.find(&format!("{FENCE}:statement")).unwrap_or(0);
        assert!(source_at < first_check);
    }

    #[test]
    fn output_drops_the_statement_line_and_counts_lines_in_the_owners_file() {
        let output = format!(
            "<source>:1:8: warning: declaration uses `sorry`\n<source>:3:4: error: Tactic `rfl` failed:\n  n : Nat\n{FENCE}:statement\nreference"
        );
        assert_eq!(
            owner_output(&output, FENCE),
            "<source>:2:4: error: Tactic `rfl` failed:\n  n : Nat"
        );
    }

    #[test]
    fn fenced_takes_the_last_open_and_the_next_close() {
        assert_eq!(fenced("a1b a2b", "a", "b"), Some("2"));
        assert_eq!(fenced("a1", "a", "b"), None);
        assert_eq!(fenced("b1a", "a", "b"), None);
        assert_eq!(fenced("ab", "a", "b"), Some(""));
        assert_eq!(fenced("xyz", "", "b"), None);
        assert_eq!(fenced("é:aé:b", "é:a", "é:b"), Some(""));
    }

    #[test]
    fn a_fence_value_is_32_hex_digits_and_changes() {
        let one = fence_value();
        assert_eq!(one.len(), 32);
        assert!(one.chars().all(|c| c.is_ascii_hexdigit()));
        assert_ne!(one, fence_value());
    }

    #[test]
    fn requests_are_checked_for_size_and_shape() {
        let good = ProveRequest {
            source: "theorem answer : True := trivial".into(),
            statement: "True".into(),
        };
        assert_eq!(good.problem(), None);
        let mut bad = good.clone();
        bad.statement = "True := trivial\ntheorem x : False".into();
        assert!(bad.problem().is_some());
        bad.statement = "True := trivial".into();
        assert!(bad.problem().is_some());
        bad = good.clone();
        bad.source = " ".into();
        assert!(bad.problem().is_some());
        bad = good.clone();
        bad.source = "a".repeat(MAX_SOURCE_BYTES + 1);
        assert!(bad.problem().is_some());
        bad = good;
        bad.statement = "a".repeat(MAX_STATEMENT_BYTES + 1);
        assert!(bad.problem().is_some());
    }
}

#[cfg(kani)]
mod proofs {
    use super::*;

    /// For every 6-byte output, what `fenced` returns lies right after an
    /// opening fence, with a closing fence right after it, and no opening
    /// fence comes later. So a report can only be read from after Lean's
    /// own last fence line, and only up to the next one.
    #[kani::proof]
    #[kani::unwind(8)]
    fn fenced_reads_only_between_the_last_open_and_the_next_close() {
        let bytes: [u8; 6] = kani::any();
        kani::assume(bytes.iter().all(|b| matches!(b, b'a' | b'b' | b'x')));
        let Ok(text) = std::str::from_utf8(&bytes) else {
            return;
        };
        if let Some(found) = fenced(text, "a", "b") {
            let start = found.as_ptr() as usize - text.as_ptr() as usize;
            let end = start + found.len();
            assert!(start >= 1);
            assert_eq!(bytes[start - 1], b'a');
            assert!(end < bytes.len());
            assert_eq!(bytes[end], b'b');
            assert!(!bytes[start..].contains(&b'a'));
            assert!(!found.as_bytes().contains(&b'b'));
        } else {
            // None only when there is no `a`, or no `b` after the last one.
            let last_a = bytes.iter().rposition(|&b| b == b'a');
            if let Some(a) = last_a {
                assert!(!bytes[a + 1..].contains(&b'b'));
            }
        }
    }
}
