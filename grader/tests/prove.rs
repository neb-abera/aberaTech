//! POST /prove over HTTP, with Compiler Explorer's Lean faked.

#![cfg(not(miri))]
#![allow(clippy::unwrap_used, missing_docs)]

mod common;

use std::net::SocketAddr;
use std::sync::Arc;
use std::time::Duration;

use common::fake::{self, Fake};
use grader::app::{Limits, router};
use grader::prove::LEAN_COMPILER;
use grader::upstream::{CompilerExplorer, Settings};
use serde_json::{Value, json};

async fn grader() -> (String, Arc<Fake>) {
    let (base, fake) = fake::spawn().await;
    let settings = Settings {
        base,
        second: Duration::from_millis(1),
        timeout: Duration::from_secs(5),
        ..Settings::default()
    };
    let app = router(CompilerExplorer::new(settings).unwrap(), Limits::default());
    let listener = tokio::net::TcpListener::bind(SocketAddr::from(([127, 0, 0, 1], 0)))
        .await
        .unwrap();
    let address = listener.local_addr().unwrap();
    tokio::spawn(async move { axum::serve(listener, app).await.unwrap() });
    (format!("http://{address}"), fake)
}

async fn prove(grader: &str, source: &str) -> (u16, Value) {
    let response = reqwest::Client::new()
        .post(format!("{grader}/prove"))
        .json(&json!({ "source": source, "statement": "∀ n : Nat, n = n" }))
        .send()
        .await
        .unwrap();
    let status = response.status().as_u16();
    (status, response.json().await.unwrap_or(Value::Null))
}

#[tokio::test]
async fn a_complete_proof_is_proved_by_the_pinned_lean() {
    let (grader, fake) = grader().await;
    let (status, body) = prove(&grader, "theorem answer : ∀ n : Nat, n = n := fun _ => rfl").await;
    assert_eq!(status, 200);
    assert_eq!(body["outcome"], "proved");
    assert_eq!(body["message"], Value::Null);
    assert_eq!(body["output"], "");
    let bodies = fake.bodies.lock().unwrap();
    assert_eq!(bodies.len(), 1);
    assert_eq!(bodies[0].0, LEAN_COMPILER);
    assert_eq!(bodies[0].1["lang"], "lean");
    assert_eq!(bodies[0].1["options"]["filters"]["execute"], false);
}

#[tokio::test]
async fn each_failure_is_named() {
    let (grader, _) = grader().await;
    for (source, outcome) in [
        ("theorem answer : ∀ n : Nat, n = n := by sorry", "sorry"),
        (
            "theorem answer : True := trivial -- fake: other",
            "statement",
        ),
        (
            "theorem answer : ∀ n : Nat, n = n := cheat -- fake: axiom",
            "axiom",
        ),
        (
            "theorem answer : ∀ n : Nat, n = n := by\n  skip -- fake: error",
            "error",
        ),
    ] {
        let (status, body) = prove(&grader, source).await;
        assert_eq!(status, 200, "{source}");
        assert_eq!(body["outcome"], outcome, "{source}");
        assert!(body["message"].is_string(), "{source}");
    }
}

#[tokio::test]
async fn lean_messages_are_counted_in_the_owners_lines() {
    let (grader, _) = grader().await;
    let (_, body) = prove(
        &grader,
        "theorem answer : ∀ n : Nat, n = n := by\n  skip -- fake: error",
    )
    .await;
    assert_eq!(body["output"], "<source>:2:2: error: unsolved goals");
}

#[tokio::test]
async fn a_forbidden_word_never_reaches_compiler_explorer() {
    let (grader, fake) = grader().await;
    let (status, body) = prove(
        &grader,
        "#eval 1\ntheorem answer : ∀ n : Nat, n = n := fun _ => rfl",
    )
    .await;
    assert_eq!(status, 200);
    assert_eq!(body["outcome"], "forbidden");
    assert_eq!(fake.calls(), 0);
}

#[tokio::test]
async fn a_malformed_request_is_refused_before_compiler_explorer() {
    let (grader, fake) = grader().await;
    let response = reqwest::Client::new()
        .post(format!("{grader}/prove"))
        .json(&json!({ "source": "theorem answer : True := trivial", "statement": "True := trivial" }))
        .send()
        .await
        .unwrap();
    assert_eq!(response.status().as_u16(), 400);
    let response = reqwest::Client::new()
        .post(format!("{grader}/prove"))
        .json(&json!({ "source": "x", "statement": "True", "extra": 1 }))
        .send()
        .await
        .unwrap();
    assert_eq!(response.status().as_u16(), 422);
    assert_eq!(fake.calls(), 0);
}

#[tokio::test]
async fn a_busy_compiler_explorer_is_a_503() {
    let (grader, fake) = grader().await;
    *fake.busy.lock().unwrap() = 10;
    let (status, body) = prove(&grader, "theorem answer : ∀ n : Nat, n = n := fun _ => rfl").await;
    assert_eq!(status, 503);
    assert!(body["error"].as_str().unwrap().contains("busy"));
}
