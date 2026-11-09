//! What the grader is asked to do, and what it answers.

use serde::{Deserialize, Serialize};

use crate::outputs::Difference;

/// The most bytes of source a drill can send.
pub const MAX_SOURCE_BYTES: usize = 16 * 1024;
/// The most tests one request can carry.
pub const MAX_TESTS: usize = 10;
/// The most bytes in one test's input or expected output.
pub const MAX_IO_BYTES: usize = 4 * 1024;
/// The most bytes of compiler flags.
pub const MAX_FLAGS_BYTES: usize = 200;
/// The longest time limit a test can set, in milliseconds.
pub const MAX_TIME_LIMIT_MS: u32 = 10_000;
/// The largest request body the grader reads. The limits above sum to
/// about 100 KiB of text, and JSON escaping can double it.
pub const MAX_BODY_BYTES: usize = 256 * 1024;
/// The most bytes of program output returned per stream.
pub const MAX_RETURNED_BYTES: usize = 8 * 1024;

/// The languages Compiler Explorer is asked to build, by its own ids.
pub const LANGUAGES: [&str; 3] = ["c++", "c", "rust"];

/// POST /grade.
#[derive(Debug, Clone, Deserialize, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct GradeRequest {
    /// The program.
    pub source: String,
    /// A Compiler Explorer language id: `c++`, `c`, or `rust`.
    pub language: String,
    /// A Compiler Explorer compiler id, such as `g162`.
    pub compiler: String,
    /// Compiler flags, such as `-O2 -std=c++23`.
    #[serde(default)]
    pub flags: String,
    /// The tests, run in order.
    pub tests: Vec<TestCase>,
}

/// One test: what goes in on stdin, what must come out, and how long it may take.
#[derive(Debug, Clone, Deserialize, Serialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct TestCase {
    /// The program's standard input.
    pub stdin: String,
    /// The standard output a correct program prints.
    pub expected: String,
    /// The longest the run may take, in milliseconds.
    pub time_limit_ms: u32,
}

impl GradeRequest {
    /// Why the grader refuses this request, or `None` when it can run it.
    ///
    /// ```
    /// use grader::request::{GradeRequest, TestCase};
    /// let request = GradeRequest {
    ///     source: "int main() {}".into(),
    ///     language: "c++".into(),
    ///     compiler: "g162".into(),
    ///     flags: "-O2".into(),
    ///     tests: vec![TestCase { stdin: String::new(), expected: String::new(), time_limit_ms: 1000 }],
    /// };
    /// assert_eq!(request.problem(), None);
    /// ```
    #[must_use]
    pub fn problem(&self) -> Option<String> {
        if self.source.trim().is_empty() {
            return Some("The source is empty.".into());
        }
        if self.source.len() > MAX_SOURCE_BYTES {
            return Some(format!(
                "The source is over {} KB.",
                MAX_SOURCE_BYTES / 1024
            ));
        }
        if !LANGUAGES.contains(&self.language.as_str()) {
            return Some(format!("The language is one of {}.", LANGUAGES.join(", ")));
        }
        if !is_compiler_id(&self.compiler) {
            return Some(
                "A compiler id is 1 to 40 letters, digits, dots, hyphens, or underscores.".into(),
            );
        }
        if self.flags.len() > MAX_FLAGS_BYTES
            || !self.flags.bytes().all(|byte| (b' '..=b'~').contains(&byte))
        {
            return Some(format!(
                "The flags are at most {MAX_FLAGS_BYTES} printable ASCII characters on one line."
            ));
        }
        if self.tests.is_empty() || self.tests.len() > MAX_TESTS {
            return Some(format!("A request has 1 to {MAX_TESTS} tests."));
        }
        for test in &self.tests {
            if test.stdin.len() > MAX_IO_BYTES || test.expected.len() > MAX_IO_BYTES {
                return Some(format!(
                    "Each test's input and output is at most {} KB.",
                    MAX_IO_BYTES / 1024
                ));
            }
            if test.time_limit_ms == 0 || test.time_limit_ms > MAX_TIME_LIMIT_MS {
                return Some(format!(
                    "A time limit is 1 to {MAX_TIME_LIMIT_MS} milliseconds."
                ));
            }
        }
        None
    }
}

/// Whether `id` is safe to put in Compiler Explorer's URL path.
#[must_use]
pub fn is_compiler_id(id: &str) -> bool {
    (1..=40).contains(&id.len())
        && id
            .bytes()
            .all(|byte| byte.is_ascii_alphanumeric() || matches!(byte, b'.' | b'-' | b'_'))
        && id != "."
        && id != ".."
}

/// What happened to one test.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum Verdict {
    /// The output matched.
    Pass,
    /// The program ran in time and printed something else.
    WrongAnswer,
    /// The program did not build, so no test ran.
    CompileError,
    /// The program crashed, exited with a code other than 0, or did not start.
    RuntimeError,
    /// The program ran past the test's time limit.
    TimeLimit,
}

/// One test's result.
#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct TestVerdict {
    /// What happened.
    pub verdict: Verdict,
    /// What the program printed, cut at 8 KiB.
    pub stdout: String,
    /// What a person reads about a failure: the compiler's errors, the exit code.
    pub message: Option<String>,
    /// For a wrong answer, the first line that differs.
    pub diff: Option<Difference>,
    /// How long the run took on Compiler Explorer, in milliseconds.
    pub exec_ms: Option<u64>,
}

/// The answer to POST /grade.
#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct GradeResponse {
    /// False when the program did not build.
    pub compiled: bool,
    /// The compiler's errors, when it did not build.
    pub compile_error: Option<String>,
    /// One verdict per test, in the request's order.
    pub tests: Vec<TestVerdict>,
}

/// `text` cut to at most `limit` bytes, on a character boundary.
#[must_use]
pub fn cut(text: &str, limit: usize) -> String {
    if text.len() <= limit {
        return text.to_owned();
    }
    let mut end = limit;
    while !text.is_char_boundary(end) {
        end -= 1;
    }
    text[..end].to_owned()
}

#[cfg(test)]
mod tests {
    use super::*;

    fn request() -> GradeRequest {
        GradeRequest {
            source: "int main() {}".into(),
            language: "c++".into(),
            compiler: "g162".into(),
            flags: "-O2 -std=c++23".into(),
            tests: vec![TestCase {
                stdin: "1 2\n".into(),
                expected: "3\n".into(),
                time_limit_ms: 2000,
            }],
        }
    }

    #[test]
    fn a_well_formed_request_has_no_problem() {
        assert_eq!(request().problem(), None);
    }

    #[test]
    fn each_limit_is_refused_one_past_it() {
        type Change = fn(&mut GradeRequest);
        let cases: Vec<(&str, Change)> = vec![
            ("The source is empty.", |r| r.source = "  \n".into()),
            ("The source is over 16 KB.", |r| {
                r.source = "x".repeat(MAX_SOURCE_BYTES + 1);
            }),
            ("The language", |r| r.language = "python".into()),
            ("A compiler id", |r| r.compiler = "../g162".into()),
            ("A compiler id", |r| r.compiler = String::new()),
            ("A compiler id", |r| r.compiler = "g".repeat(41)),
            ("The flags", |r| r.flags = "-O2\n-g".into()),
            ("The flags", |r| r.flags = "x".repeat(MAX_FLAGS_BYTES + 1)),
            ("A request has 1 to 10 tests.", |r| r.tests.clear()),
            ("A request has 1 to 10 tests.", |r| {
                r.tests = vec![r.tests[0].clone(); MAX_TESTS + 1];
            }),
            ("Each test's input", |r| {
                r.tests[0].stdin = "1".repeat(MAX_IO_BYTES + 1);
            }),
            ("Each test's input", |r| {
                r.tests[0].expected = "1".repeat(MAX_IO_BYTES + 1);
            }),
            ("A time limit", |r| r.tests[0].time_limit_ms = 0),
            ("A time limit", |r| {
                r.tests[0].time_limit_ms = MAX_TIME_LIMIT_MS + 1;
            }),
        ];
        for (expected, change) in cases {
            let mut refused = request();
            change(&mut refused);
            let problem = refused.problem().unwrap_or_default();
            assert!(
                problem.starts_with(expected),
                "{expected:?} but got {problem:?}"
            );
        }
    }

    #[test]
    fn each_limit_is_accepted_at_it() {
        let mut request = request();
        request.source = "x".repeat(MAX_SOURCE_BYTES);
        request.compiler = "g".repeat(40);
        request.flags = "x".repeat(MAX_FLAGS_BYTES);
        request.tests = vec![
            TestCase {
                stdin: "1".repeat(MAX_IO_BYTES),
                expected: "1".repeat(MAX_IO_BYTES),
                time_limit_ms: MAX_TIME_LIMIT_MS,
            };
            MAX_TESTS
        ];
        assert_eq!(request.problem(), None);
    }

    #[test]
    fn a_compiler_id_cannot_climb_the_path() {
        assert!(is_compiler_id("clang_trunk"));
        assert!(is_compiler_id("g16.2"));
        assert!(!is_compiler_id("."));
        assert!(!is_compiler_id(".."));
        assert!(!is_compiler_id("g162/../x"));
        assert!(!is_compiler_id("g162?x=1"));
    }

    #[test]
    fn cut_keeps_whole_characters() {
        assert_eq!(cut("abc", 8), "abc");
        assert_eq!(cut("abcdef", 3), "abc");
        assert_eq!(cut("aé", 2), "a");
    }

    #[test]
    fn unknown_fields_are_refused() {
        let body = r#"{"source":"x","language":"c++","compiler":"g162","tests":[],"extra":1}"#;
        assert!(serde_json::from_str::<GradeRequest>(body).is_err());
    }

    #[test]
    fn verdicts_serialize_in_snake_case() {
        let names: Vec<String> = [
            Verdict::Pass,
            Verdict::WrongAnswer,
            Verdict::CompileError,
            Verdict::RuntimeError,
            Verdict::TimeLimit,
        ]
        .iter()
        .map(|verdict| serde_json::to_string(verdict).unwrap_or_default())
        .collect();
        assert_eq!(
            names,
            [
                "\"pass\"",
                "\"wrong_answer\"",
                "\"compile_error\"",
                "\"runtime_error\"",
                "\"time_limit\""
            ]
        );
    }
}
