//! Turning Compiler Explorer's runs into verdicts.

use crate::outputs::{first_difference, outputs_match};
use crate::request::{
    GradeRequest, GradeResponse, MAX_RETURNED_BYTES, TestCase, TestVerdict, Verdict, cut,
};
use crate::upstream::{CompilerExplorer, Execution, UpstreamError, joined};

/// The compiler's messages when `run` did not build, or `None` when it did.
#[must_use]
pub fn build_failure(run: &Execution) -> Option<String> {
    let build = run.build_result.as_ref()?;
    if build.code == 0 {
        return None;
    }
    let errors = joined(&build.stderr);
    Some(if errors.trim().is_empty() {
        "It did not compile.".into()
    } else {
        cut(&errors, MAX_RETURNED_BYTES)
    })
}

/// The verdict for one test whose program built.
///
/// A run past the limit is a time limit whatever its exit code, because a
/// killed program exits with one. A program that did not start, or exited
/// other than 0, is a runtime error. Otherwise its output decides.
#[must_use]
pub fn judge(test: &TestCase, run: &Execution) -> TestVerdict {
    let stdout = joined(&run.stdout);
    let over = run
        .exec_time
        .is_some_and(|ms| ms > u64::from(test.time_limit_ms));
    let (verdict, message) = if run.timed_out || over {
        (
            Verdict::TimeLimit,
            Some(format!("It ran past the {} ms limit.", test.time_limit_ms)),
        )
    } else if !run.did_execute {
        (Verdict::RuntimeError, Some("It did not run.".into()))
    } else if run.code != 0 {
        let stderr = joined(&run.stderr);
        let exited = format!("It exited with code {}. {}", run.code, stderr);
        (
            Verdict::RuntimeError,
            Some(cut(exited.trim(), MAX_RETURNED_BYTES)),
        )
    } else if outputs_match(&test.expected, &stdout) {
        (Verdict::Pass, None)
    } else {
        (Verdict::WrongAnswer, None)
    };
    let diff = (verdict == Verdict::WrongAnswer)
        .then(|| first_difference(&test.expected, &stdout))
        .flatten();
    TestVerdict {
        verdict,
        stdout: cut(&stdout, MAX_RETURNED_BYTES),
        message,
        diff,
        exec_ms: run.exec_time,
    }
}

/// Run every test of `request` on Compiler Explorer and judge each.
///
/// When the first build fails, no other test is sent, and every test is a
/// compile error with the compiler's messages.
///
/// # Errors
///
/// When Compiler Explorer gives no run to judge.
pub async fn grade(
    explorer: &CompilerExplorer,
    request: &GradeRequest,
) -> Result<GradeResponse, UpstreamError> {
    let mut tests = Vec::with_capacity(request.tests.len());
    for test in &request.tests {
        let run = explorer.execute(request, &test.stdin).await?;
        if let Some(errors) = build_failure(&run) {
            return Ok(compile_error(request, errors));
        }
        tests.push(judge(test, &run));
    }
    Ok(GradeResponse {
        compiled: true,
        compile_error: None,
        tests,
    })
}

fn compile_error(request: &GradeRequest, errors: String) -> GradeResponse {
    let tests = request
        .tests
        .iter()
        .map(|_| TestVerdict {
            verdict: Verdict::CompileError,
            stdout: String::new(),
            message: Some(errors.clone()),
            diff: None,
            exec_ms: None,
        })
        .collect();
    GradeResponse {
        compiled: false,
        compile_error: Some(errors),
        tests,
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::upstream::{Build, Line};

    fn test(expected: &str) -> TestCase {
        TestCase {
            stdin: String::new(),
            expected: expected.into(),
            time_limit_ms: 1000,
        }
    }

    fn lines(text: &str) -> Vec<Line> {
        text.split('\n')
            .map(|line| Line { text: line.into() })
            .collect()
    }

    fn ran(stdout: &str) -> Execution {
        Execution {
            did_execute: true,
            stdout: lines(stdout),
            exec_time: Some(10),
            build_result: Some(Build::default()),
            ..Execution::default()
        }
    }

    #[test]
    fn matching_output_passes() {
        let verdict = judge(&test("5\n"), &ran("5"));
        assert_eq!(verdict.verdict, Verdict::Pass);
        assert_eq!(verdict.diff, None);
        assert_eq!(verdict.message, None);
        assert_eq!(verdict.exec_ms, Some(10));
    }

    #[test]
    fn other_output_is_a_wrong_answer_with_the_line() {
        let verdict = judge(&test("1\n2\n"), &ran("1\n3"));
        assert_eq!(verdict.verdict, Verdict::WrongAnswer);
        let diff = verdict.diff.unwrap_or_else(|| panic!("no diff"));
        assert_eq!(diff.line, 2);
        assert_eq!(diff.actual.as_deref(), Some("3"));
        assert_eq!(verdict.stdout, "1\n3");
    }

    #[test]
    fn a_nonzero_exit_is_a_runtime_error_with_stderr() {
        let mut run = ran("5");
        run.code = 139;
        run.stderr = lines("Segmentation fault");
        let verdict = judge(&test("5"), &run);
        assert_eq!(verdict.verdict, Verdict::RuntimeError);
        assert_eq!(
            verdict.message.as_deref(),
            Some("It exited with code 139. Segmentation fault")
        );
    }

    #[test]
    fn a_program_that_never_ran_is_a_runtime_error() {
        let mut run = ran("");
        run.did_execute = false;
        assert_eq!(judge(&test(""), &run).verdict, Verdict::RuntimeError);
    }

    #[test]
    fn past_the_limit_is_a_time_limit_even_with_the_right_answer() {
        let mut run = ran("5");
        run.exec_time = Some(1001);
        assert_eq!(judge(&test("5"), &run).verdict, Verdict::TimeLimit);
        run.exec_time = Some(1000);
        assert_eq!(judge(&test("5"), &run).verdict, Verdict::Pass);
    }

    #[test]
    fn a_kill_for_time_is_a_time_limit_not_a_crash() {
        let mut run = ran("");
        run.timed_out = true;
        run.code = -1;
        let verdict = judge(&test("5"), &run);
        assert_eq!(verdict.verdict, Verdict::TimeLimit);
        assert_eq!(
            verdict.message.as_deref(),
            Some("It ran past the 1000 ms limit.")
        );
    }

    #[test]
    fn a_failed_build_returns_its_errors() {
        let mut run = ran("");
        run.build_result = Some(Build {
            code: 1,
            stderr: lines("<source>:1:2: error: #error"),
        });
        assert_eq!(
            build_failure(&run).as_deref(),
            Some("<source>:1:2: error: #error")
        );
        run.build_result = Some(Build {
            code: 1,
            stderr: Vec::new(),
        });
        assert_eq!(build_failure(&run).as_deref(), Some("It did not compile."));
        assert_eq!(build_failure(&ran("")), None);
        assert_eq!(build_failure(&Execution::default()), None);
    }
}
