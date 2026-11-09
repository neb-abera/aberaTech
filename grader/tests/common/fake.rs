//! Compiler Explorer in memory, for the tests and the end-to-end suite.
//!
//! It runs nothing. The source decides what it answers, the same way the
//! .NET server's FakeCompilerExplorer.cs does:
//!
//! - `#error` fails to build.
//! - `// fake: sum` prints the sum of the integers on stdin.
//! - `// fake: echo` prints stdin.
//! - `// fake: crash` exits 139 with "Segmentation fault".
//! - `// fake: slow` takes 5000 ms and prints nothing.
//! - `// fake: hang` is stopped for time.
//! - Anything else prints nothing.
//!
//! `delay_ms` waits that long before each answer. `busy` answers 429 with Retry-After: 1 that many times first. `status`
//! answers that status to every call. `garbage` answers 200 with no JSON.

#![allow(dead_code, clippy::unwrap_used, missing_docs)]

use std::net::SocketAddr;
use std::sync::{Arc, Mutex};

use axum::extract::{Path, State};
use axum::http::{HeaderValue, StatusCode};
use axum::response::{IntoResponse, Response};
use axum::routing::post;
use axum::{Json, Router};
use serde_json::{Value, json};

#[derive(Debug, Default)]
pub struct Fake {
    pub bodies: Mutex<Vec<(String, Value)>>,
    pub busy: Mutex<usize>,
    pub status: Mutex<Option<u16>>,
    pub garbage: Mutex<bool>,
    pub delay_ms: Mutex<u64>,
}

impl Fake {
    pub fn calls(&self) -> usize {
        self.bodies.lock().unwrap().len()
    }
}

fn sum(stdin: &str) -> i64 {
    stdin
        .split(|c: char| !(c.is_ascii_digit() || c == '-'))
        .filter_map(|word| word.parse::<i64>().ok())
        .sum()
}

fn lines(text: &str) -> Value {
    if text.is_empty() {
        return json!([]);
    }
    Value::Array(text.lines().map(|line| json!({ "text": line })).collect())
}

pub fn answer(source: &str, stdin: &str) -> Value {
    if source.contains("#error") {
        return json!({
            "code": -1, "didExecute": false, "stdout": [], "stderr": [],
            "buildResult": { "code": 1, "stderr": [{ "text": "<source>:1:2: error: #error" }] }
        });
    }
    let (code, stdout, stderr, exec_time, timed_out) = if source.contains("// fake: sum") {
        (0, sum(stdin).to_string(), String::new(), 3, false)
    } else if source.contains("// fake: echo") {
        (0, stdin.to_owned(), String::new(), 3, false)
    } else if source.contains("// fake: crash") {
        (
            139,
            String::new(),
            "Segmentation fault".to_owned(),
            3,
            false,
        )
    } else if source.contains("// fake: slow") {
        (0, String::new(), String::new(), 5000, false)
    } else if source.contains("// fake: hang") {
        (-1, String::new(), String::new(), 10_000, true)
    } else {
        (0, String::new(), String::new(), 3, false)
    };
    json!({
        "code": code, "didExecute": true, "timedOut": timed_out, "execTime": exec_time,
        "stdout": lines(&stdout), "stderr": lines(&stderr),
        "buildResult": { "code": 0, "stderr": [] }
    })
}

async fn compile(
    State(fake): State<Arc<Fake>>,
    Path(compiler): Path<String>,
    Json(body): Json<Value>,
) -> Response {
    fake.bodies.lock().unwrap().push((compiler, body.clone()));
    let delay = *fake.delay_ms.lock().unwrap();
    if delay > 0 {
        tokio::time::sleep(std::time::Duration::from_millis(delay)).await;
    }
    {
        let mut busy = fake.busy.lock().unwrap();
        if *busy > 0 {
            *busy -= 1;
            let mut response = StatusCode::TOO_MANY_REQUESTS.into_response();
            response
                .headers_mut()
                .insert("retry-after", HeaderValue::from_static("1"));
            return response;
        }
    }
    if let Some(status) = *fake.status.lock().unwrap() {
        return StatusCode::from_u16(status).unwrap().into_response();
    }
    if *fake.garbage.lock().unwrap() {
        return "<html>not json</html>".into_response();
    }
    let source = body["source"].as_str().unwrap_or_default();
    let stdin = body["options"]["executeParameters"]["stdin"]
        .as_str()
        .unwrap_or_default();
    Json(answer(source, stdin)).into_response()
}

pub fn router(fake: Arc<Fake>) -> Router {
    Router::new()
        .route("/api/compiler/{compiler}/compile", post(compile))
        .with_state(fake)
}

/// The fake on a free port of 127.0.0.1, and its address.
pub async fn spawn() -> (String, Arc<Fake>) {
    let fake = Arc::new(Fake::default());
    let listener = tokio::net::TcpListener::bind(SocketAddr::from(([127, 0, 0, 1], 0)))
        .await
        .unwrap();
    let address = listener.local_addr().unwrap();
    let app = router(fake.clone());
    tokio::spawn(async move { axum::serve(listener, app).await.unwrap() });
    (format!("http://{address}"), fake)
}
