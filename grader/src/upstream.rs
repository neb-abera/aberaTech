//! Compiler Explorer's public API: the only place submitted code runs.
//!
//! One request per test. Compiler Explorer caches the build, so only the
//! first compiles. The API is documented in
//! github.com/compiler-explorer/compiler-explorer, docs/API.md.

use std::sync::Arc;
use std::time::Duration;

use reqwest::StatusCode;
use serde::Deserialize;
use serde_json::json;
use tokio::sync::Semaphore;

use crate::request::GradeRequest;

/// The most bytes read from one Compiler Explorer answer.
pub const MAX_ANSWER_BYTES: usize = 1024 * 1024;

/// Why Compiler Explorer gave no run to judge.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum UpstreamError {
    /// It answered 429 every time, or asked for a wait longer than the grader allows.
    Busy,
    /// It answered with this status.
    Status(u16),
    /// It did not answer: no connection, or no answer in time.
    Unreachable,
    /// Its answer was not JSON in the shape of a run, or was too large.
    Malformed,
}

impl UpstreamError {
    /// What the caller is told.
    #[must_use]
    pub fn message(self) -> String {
        match self {
            Self::Busy => "Compiler Explorer is busy. Try again in a minute.".into(),
            Self::Status(code) => format!("Compiler Explorer answered {code}."),
            Self::Unreachable => "Compiler Explorer did not answer.".into(),
            Self::Malformed => "Compiler Explorer sent an answer the grader could not read.".into(),
        }
    }
}

/// One line of Compiler Explorer output.
#[derive(Debug, Clone, Default, Deserialize)]
pub struct Line {
    /// The text, without its newline.
    #[serde(default)]
    pub text: String,
}

/// The build half of an answer.
#[derive(Debug, Clone, Default, Deserialize)]
pub struct Build {
    /// 0 when it built.
    #[serde(default)]
    pub code: i64,
    /// The compiler's messages.
    #[serde(default)]
    pub stderr: Vec<Line>,
}

/// One compile and run, as Compiler Explorer reports it.
#[derive(Debug, Clone, Default, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Execution {
    /// The program's exit code.
    #[serde(default)]
    pub code: i64,
    /// Whether the program ran at all.
    #[serde(default)]
    pub did_execute: bool,
    /// Whether Compiler Explorer stopped it for running too long.
    #[serde(default)]
    pub timed_out: bool,
    /// Standard output, by line.
    #[serde(default)]
    pub stdout: Vec<Line>,
    /// Standard error, by line.
    #[serde(default)]
    pub stderr: Vec<Line>,
    /// How long it ran, in milliseconds.
    #[serde(default)]
    pub exec_time: Option<u64>,
    /// The build.
    #[serde(default)]
    pub build_result: Option<Build>,
}

/// Lines joined with `\n`.
#[must_use]
pub fn joined(lines: &[Line]) -> String {
    lines
        .iter()
        .map(|line| line.text.as_str())
        .collect::<Vec<_>>()
        .join("\n")
}

/// How the client behaves. The defaults are production's.
#[derive(Debug, Clone)]
pub struct Settings {
    /// Where Compiler Explorer is, without a trailing slash.
    pub base: String,
    /// Calls in flight at once, across every request.
    pub concurrency: usize,
    /// Tries per call when it answers 429.
    pub attempts: u32,
    /// One second of Retry-After, as waited. Tests shorten it.
    pub second: Duration,
    /// The longest Retry-After the grader waits out, in seconds.
    pub max_wait_seconds: u64,
    /// How long one call may take.
    pub timeout: Duration,
}

impl Default for Settings {
    fn default() -> Self {
        Self {
            base: "https://godbolt.org".into(),
            concurrency: 4,
            attempts: 3,
            second: Duration::from_secs(1),
            max_wait_seconds: 10,
            timeout: Duration::from_secs(30),
        }
    }
}

/// The seconds to wait after try `attempt` (from 1) answered 429, or
/// `None` when the grader gives up. Retry-After when it is given, else 2
/// then 4 seconds.
#[must_use]
pub fn backoff(settings: &Settings, attempt: u32, retry_after: Option<u64>) -> Option<u64> {
    if attempt >= settings.attempts {
        return None;
    }
    let wait = retry_after.unwrap_or_else(|| 2 * u64::from(attempt));
    (wait <= settings.max_wait_seconds).then_some(wait)
}

/// A Compiler Explorer client with a concurrency limit and 429 backoff.
#[derive(Debug, Clone)]
pub struct CompilerExplorer {
    client: reqwest::Client,
    settings: Settings,
    permits: Arc<Semaphore>,
}

impl CompilerExplorer {
    /// A client for `settings`.
    ///
    /// # Errors
    ///
    /// When the HTTP client cannot be built, such as when TLS cannot load.
    pub fn new(settings: Settings) -> Result<Self, reqwest::Error> {
        // ring is the one TLS provider in the build. A second call finds it
        // installed already, which is fine.
        let _ = rustls::crypto::ring::default_provider().install_default();
        let client = reqwest::Client::builder()
            .user_agent("abera.tech-grader (https://abera.tech)")
            .connect_timeout(Duration::from_secs(5))
            .timeout(settings.timeout)
            .build()?;
        let permits = Arc::new(Semaphore::new(settings.concurrency.max(1)));
        Ok(Self {
            client,
            settings,
            permits,
        })
    }

    /// Compile `request.source` and run it on `stdin`.
    ///
    /// # Errors
    ///
    /// When Compiler Explorer gives no run to judge (see [`UpstreamError`]).
    pub async fn execute(
        &self,
        request: &GradeRequest,
        stdin: &str,
    ) -> Result<Execution, UpstreamError> {
        let url = format!(
            "{}/api/compiler/{}/compile",
            self.settings.base, request.compiler
        );
        let body = json!({
            "source": request.source,
            "compiler": request.compiler,
            "options": {
                "userArguments": request.flags,
                "executeParameters": { "args": [], "stdin": stdin },
                "compilerOptions": { "executorRequest": true },
                "filters": { "execute": true },
                "tools": [],
                "libraries": [],
            },
            "lang": request.language,
            "allowStoreCodeDebug": false,
        });

        let mut attempt = 1;
        loop {
            let response = {
                let _permit = self
                    .permits
                    .acquire()
                    .await
                    .map_err(|_| UpstreamError::Unreachable)?;
                self.client
                    .post(&url)
                    .header("Accept", "application/json")
                    .json(&body)
                    .send()
                    .await
                    .map_err(|_| UpstreamError::Unreachable)?
            };
            let status = response.status();
            if status == StatusCode::TOO_MANY_REQUESTS {
                let retry_after = response
                    .headers()
                    .get("retry-after")
                    .and_then(|value| value.to_str().ok())
                    .and_then(|value| value.trim().parse::<u64>().ok());
                let Some(wait) = backoff(&self.settings, attempt, retry_after) else {
                    tracing::warn!(attempt, "compiler explorer busy, giving up");
                    return Err(UpstreamError::Busy);
                };
                tracing::info!(attempt, wait, "compiler explorer busy, waiting");
                tokio::time::sleep(
                    self.settings
                        .second
                        .saturating_mul(u32::try_from(wait).unwrap_or(u32::MAX)),
                )
                .await;
                attempt += 1;
                continue;
            }
            if !status.is_success() {
                tracing::warn!(status = status.as_u16(), "compiler explorer refused");
                return Err(UpstreamError::Status(status.as_u16()));
            }
            let bytes = read_capped(response).await?;
            return serde_json::from_slice(&bytes).map_err(|_| UpstreamError::Malformed);
        }
    }
}

/// The body, refused past [`MAX_ANSWER_BYTES`].
async fn read_capped(mut response: reqwest::Response) -> Result<Vec<u8>, UpstreamError> {
    let mut bytes = Vec::new();
    while let Some(chunk) = response
        .chunk()
        .await
        .map_err(|_| UpstreamError::Unreachable)?
    {
        if bytes.len() + chunk.len() > MAX_ANSWER_BYTES {
            return Err(UpstreamError::Malformed);
        }
        bytes.extend_from_slice(&chunk);
    }
    Ok(bytes)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn a_real_answer_parses() {
        // Compiler Explorer's answer for a program that printed 3 and exited
        // 3, from g162 on 9 Nov 2026, trimmed.
        let answer = r#"{"code":3,"okToCache":true,"timedOut":false,"stdout":[{"text":"3"}],
            "stderr":[],"truncated":false,"execTime":18,"didExecute":true,
            "buildResult":{"code":0,"timedOut":false,"stderr":[],"execTime":94},"queueTime":5}"#;
        let run: Execution = serde_json::from_str(answer).unwrap_or_default();
        assert_eq!(run.code, 3);
        assert!(run.did_execute);
        assert_eq!(run.exec_time, Some(18));
        assert_eq!(joined(&run.stdout), "3");
        assert_eq!(run.build_result.map(|build| build.code), Some(0));
    }

    #[test]
    fn backoff_follows_retry_after_then_gives_up() {
        let settings = Settings::default();
        assert_eq!(backoff(&settings, 1, Some(1)), Some(1));
        assert_eq!(backoff(&settings, 1, None), Some(2));
        assert_eq!(backoff(&settings, 2, None), Some(4));
        assert_eq!(backoff(&settings, 3, None), None);
        assert_eq!(backoff(&settings, 1, Some(11)), None);
        assert_eq!(backoff(&settings, 1, Some(10)), Some(10));
    }

    #[test]
    fn every_error_says_what_happened() {
        assert!(UpstreamError::Busy.message().contains("busy"));
        assert!(UpstreamError::Status(502).message().contains("502"));
        assert!(
            UpstreamError::Unreachable
                .message()
                .contains("did not answer")
        );
        assert!(
            UpstreamError::Malformed
                .message()
                .contains("could not read")
        );
    }
}
