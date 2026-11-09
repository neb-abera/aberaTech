//! POST /grade and GET /healthz over HTTP, with Compiler Explorer faked.

#![cfg(not(miri))]
#![allow(clippy::unwrap_used, missing_docs)]

mod common;

use std::net::SocketAddr;
use std::sync::Arc;
use std::time::Duration;

use common::fake::{self, Fake};
use grader::app::{Limits, router};
use grader::upstream::{CompilerExplorer, Settings};
use serde_json::{Value, json};

async fn grader_with(base: &str, limits: Limits) -> String {
    let settings = Settings {
        base: base.to_owned(),
        second: Duration::from_millis(1),
        timeout: Duration::from_secs(5),
        ..Settings::default()
    };
    let app = router(CompilerExplorer::new(settings).unwrap(), limits);
    let listener = tokio::net::TcpListener::bind(SocketAddr::from(([127, 0, 0, 1], 0)))
        .await
        .unwrap();
    let address = listener.local_addr().unwrap();
    tokio::spawn(async move { axum::serve(listener, app).await.unwrap() });
    format!("http://{address}")
}

async fn grader() -> (String, Arc<Fake>) {
    let (base, fake) = fake::spawn().await;
    (grader_with(&base, Limits::default()).await, fake)
}

fn drill(source: &str) -> Value {
    json!({
        "source": source,
        "language": "c++",
        "compiler": "g162",
        "flags": "-O2 -std=c++23",
        "tests": [
            { "stdin": "2 3\n", "expected": "5\n", "timeLimitMs": 1000 },
            { "stdin": "-5 5\n", "expected": "0  \n\n", "timeLimitMs": 1000 },
            { "stdin": "1 1\n", "expected": "3\n", "timeLimitMs": 1000 },
        ],
    })
}

async fn post(grader: &str, body: &Value) -> (u16, Value) {
    let response = reqwest::Client::new()
        .post(format!("{grader}/grade"))
        .json(body)
        .send()
        .await
        .unwrap();
    let status = response.status().as_u16();
    (status, response.json().await.unwrap_or(Value::Null))
}

fn verdicts(body: &Value) -> Vec<&str> {
    body["tests"]
        .as_array()
        .unwrap()
        .iter()
        .map(|test| test["verdict"].as_str().unwrap())
        .collect()
}

#[tokio::test]
async fn health_answers_ok() {
    let (grader, _) = grader().await;
    let response = reqwest::get(format!("{grader}/healthz")).await.unwrap();
    assert_eq!(response.status().as_u16(), 200);
    assert_eq!(response.text().await.unwrap(), "ok");
}

#[tokio::test]
async fn the_probe_sees_a_healthy_grader_and_a_closed_port() {
    let (grader, _) = grader().await;
    let address: SocketAddr = grader.trim_start_matches("http://").parse().unwrap();
    assert!(
        tokio::task::spawn_blocking(move || grader::probe::healthy(address))
            .await
            .unwrap()
    );
    let closed = std::net::TcpListener::bind("127.0.0.1:0")
        .unwrap()
        .local_addr()
        .unwrap();
    assert!(!grader::probe::healthy(closed));
}

#[tokio::test]
async fn each_test_is_judged_on_its_output() {
    let (grader, fake) = grader().await;
    let (status, body) = post(&grader, &drill("int main() {} // fake: sum")).await;
    assert_eq!(status, 200);
    assert_eq!(body["compiled"], true);
    assert_eq!(verdicts(&body), ["pass", "pass", "wrong_answer"]);
    assert_eq!(
        body["tests"][2]["diff"],
        json!({ "line": 1, "expected": "3", "actual": "2" })
    );
    assert_eq!(body["tests"][2]["stdout"], "2");
    assert_eq!(body["tests"][0]["execMs"], 3);

    // What Compiler Explorer was sent: the compiler in the path and the
    // body, the flags, the language, and each test's stdin.
    let bodies = fake.bodies.lock().unwrap().clone();
    assert_eq!(bodies.len(), 3);
    let (compiler, sent) = &bodies[1];
    assert_eq!(compiler, "g162");
    assert_eq!(sent["compiler"], "g162");
    assert_eq!(sent["lang"], "c++");
    assert_eq!(sent["options"]["userArguments"], "-O2 -std=c++23");
    assert_eq!(sent["options"]["executeParameters"]["stdin"], "-5 5\n");
    assert_eq!(sent["options"]["filters"]["execute"], true);
    assert_eq!(sent["allowStoreCodeDebug"], false);
}

#[tokio::test]
async fn a_failed_build_stops_after_one_call() {
    let (grader, fake) = grader().await;
    let (status, body) = post(&grader, &drill("#error broken")).await;
    assert_eq!(status, 200);
    assert_eq!(body["compiled"], false);
    assert_eq!(body["compileError"], "<source>:1:2: error: #error");
    assert_eq!(verdicts(&body), ["compile_error"; 3]);
    assert_eq!(body["tests"][0]["message"], "<source>:1:2: error: #error");
    assert_eq!(fake.calls(), 1);
}

#[tokio::test]
async fn a_crash_is_a_runtime_error_and_a_slow_run_a_time_limit() {
    let (grader, _) = grader().await;
    let (_, crashed) = post(&grader, &drill("// fake: crash")).await;
    assert_eq!(verdicts(&crashed), ["runtime_error"; 3]);
    assert_eq!(
        crashed["tests"][0]["message"],
        "It exited with code 139. Segmentation fault"
    );
    let (_, slow) = post(&grader, &drill("// fake: slow")).await;
    assert_eq!(verdicts(&slow), ["time_limit"; 3]);
    let (_, hung) = post(&grader, &drill("// fake: hang")).await;
    assert_eq!(verdicts(&hung), ["time_limit"; 3]);
    assert_eq!(
        hung["tests"][0]["message"],
        "It ran past the 1000 ms limit."
    );
}

#[tokio::test]
async fn a_429_is_waited_out() {
    let (grader, fake) = grader().await;
    *fake.busy.lock().unwrap() = 2;
    let (status, body) = post(&grader, &drill("// fake: sum")).await;
    assert_eq!(status, 200);
    assert_eq!(verdicts(&body), ["pass", "pass", "wrong_answer"]);
    assert_eq!(fake.calls(), 5);
}

#[tokio::test]
async fn three_429s_in_a_row_answer_busy() {
    let (grader, fake) = grader().await;
    *fake.busy.lock().unwrap() = 3;
    let (status, body) = post(&grader, &drill("// fake: sum")).await;
    assert_eq!(status, 503);
    assert_eq!(
        body["error"],
        "Compiler Explorer is busy. Try again in a minute."
    );
    assert_eq!(fake.calls(), 3);
}

#[tokio::test]
async fn an_upstream_failure_is_a_bad_gateway() {
    let (grader, fake) = grader().await;
    *fake.status.lock().unwrap() = Some(500);
    let (status, body) = post(&grader, &drill("// fake: sum")).await;
    assert_eq!(
        (status, body["error"].as_str()),
        (502, Some("Compiler Explorer answered 500."))
    );

    *fake.status.lock().unwrap() = None;
    *fake.garbage.lock().unwrap() = true;
    let (status, body) = post(&grader, &drill("// fake: sum")).await;
    assert_eq!(status, 502);
    assert_eq!(
        body["error"],
        "Compiler Explorer sent an answer the grader could not read."
    );
}

#[tokio::test]
async fn an_unreachable_upstream_is_a_bad_gateway() {
    let closed = std::net::TcpListener::bind("127.0.0.1:0")
        .unwrap()
        .local_addr()
        .unwrap();
    let grader = grader_with(&format!("http://{closed}"), Limits::default()).await;
    let (status, body) = post(&grader, &drill("// fake: sum")).await;
    assert_eq!(status, 502);
    assert_eq!(body["error"], "Compiler Explorer did not answer.");
}

#[tokio::test]
async fn a_bad_request_costs_compiler_explorer_nothing() {
    let (grader, fake) = grader().await;
    let mut python = drill("print(1)");
    python["language"] = json!("python");
    let (status, body) = post(&grader, &python).await;
    assert_eq!(status, 400);
    assert_eq!(body["error"], "The language is one of c++, c, rust.");

    let mut climbing = drill("x");
    climbing["compiler"] = json!("../../api");
    assert_eq!(post(&grader, &climbing).await.0, 400);

    let mut extra = drill("x");
    extra["extra"] = json!(1);
    assert_eq!(post(&grader, &extra).await.0, 422);

    let client = reqwest::Client::new();
    let not_json = client
        .post(format!("{grader}/grade"))
        .header("content-type", "application/json")
        .body("{")
        .send()
        .await
        .unwrap();
    assert_eq!(not_json.status().as_u16(), 400);
    let no_type = client
        .post(format!("{grader}/grade"))
        .body("{}")
        .send()
        .await
        .unwrap();
    assert_eq!(no_type.status().as_u16(), 415);
    let huge = client
        .post(format!("{grader}/grade"))
        .header("content-type", "application/json")
        .body(vec![b' '; grader::request::MAX_BODY_BYTES + 1])
        .send()
        .await
        .unwrap();
    assert_eq!(huge.status().as_u16(), 413);
    assert_eq!(fake.calls(), 0);
}

#[tokio::test]
async fn an_unknown_route_is_404() {
    let (grader, _) = grader().await;
    let response = reqwest::get(format!("{grader}/robots.txt")).await.unwrap();
    assert_eq!(response.status().as_u16(), 404);
    let response = reqwest::get(format!("{grader}/grade")).await.unwrap();
    assert_eq!(response.status().as_u16(), 405);
}

#[tokio::test]
async fn a_grading_past_capacity_is_refused_at_once() {
    let (base, fake) = fake::spawn().await;
    *fake.delay_ms.lock().unwrap() = 400;
    let grader = grader_with(
        &base,
        Limits {
            gradings: 1,
            ..Limits::default()
        },
    )
    .await;
    let mut one = drill("// fake: sum");
    one["tests"] = json!([{ "stdin": "1", "expected": "1", "timeLimitMs": 1000 }]);
    let first = {
        let (grader, one) = (grader.clone(), one.clone());
        tokio::spawn(async move { post(&grader, &one).await })
    };
    tokio::time::sleep(Duration::from_millis(100)).await;
    let (status, body) = post(&grader, &one).await;
    assert_eq!(status, 503);
    assert_eq!(body["error"], "The grader is busy. Try again in a minute.");
    assert_eq!(first.await.unwrap().0, 200);
}

#[tokio::test]
async fn a_grading_past_its_time_is_cut_off() {
    let (base, fake) = fake::spawn().await;
    *fake.delay_ms.lock().unwrap() = 500;
    let grader = grader_with(
        &base,
        Limits {
            grading_timeout: Duration::from_millis(100),
            ..Limits::default()
        },
    )
    .await;
    let (status, body) = post(&grader, &drill("// fake: sum")).await;
    assert_eq!(status, 504);
    assert_eq!(body["error"], "Grading took too long.");
}
