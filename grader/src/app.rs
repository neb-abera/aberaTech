//! The HTTP surface: POST /grade and GET /healthz.

use std::sync::Arc;
use std::time::{Duration, Instant};

use axum::error_handling::HandleErrorLayer;
use axum::extract::rejection::JsonRejection;
use axum::extract::{DefaultBodyLimit, State};
use axum::http::StatusCode;
use axum::response::{IntoResponse, Response};
use axum::routing::{get, post};
use axum::{BoxError, Json, Router};
use serde_json::json;
use tower::ServiceBuilder;
use tower_http::trace::{DefaultOnResponse, TraceLayer};
use tracing::Level;

use crate::grade::grade;
use crate::request::{GradeRequest, MAX_BODY_BYTES, Verdict};
use crate::upstream::{CompilerExplorer, UpstreamError};

/// How the HTTP surface behaves. The defaults are production's.
#[derive(Debug, Clone, Copy)]
pub struct Limits {
    /// Gradings in flight at once. More are refused with 503 at once.
    pub gradings: usize,
    /// How long one grading may take. Ten tests at 30 s each would be
    /// longer, so a slow Compiler Explorer ends the request here.
    pub grading_timeout: Duration,
}

impl Default for Limits {
    fn default() -> Self {
        Self {
            gradings: 8,
            grading_timeout: Duration::from_secs(90),
        }
    }
}

/// The service.
pub fn router(explorer: CompilerExplorer, limits: Limits) -> Router {
    let grading = post(grade_route).layer(
        ServiceBuilder::new()
            .layer(HandleErrorLayer::new(refused))
            .load_shed()
            .concurrency_limit(limits.gradings.max(1))
            .timeout(limits.grading_timeout),
    );
    Router::new()
        .route("/healthz", get(|| async { "ok" }))
        .route("/grade", grading)
        .fallback(|| async { error(StatusCode::NOT_FOUND, "No such route.") })
        .layer(DefaultBodyLimit::max(MAX_BODY_BYTES))
        .layer(TraceLayer::new_for_http().on_response(DefaultOnResponse::new().level(Level::INFO)))
        .with_state(Arc::new(explorer))
}

fn error(status: StatusCode, message: &str) -> Response {
    (status, Json(json!({ "error": message }))).into_response()
}

async fn refused(failure: BoxError) -> Response {
    if failure.is::<tower::timeout::error::Elapsed>() {
        tracing::warn!("grading timed out");
        error(StatusCode::GATEWAY_TIMEOUT, "Grading took too long.")
    } else {
        tracing::warn!("grader at capacity, refused");
        error(
            StatusCode::SERVICE_UNAVAILABLE,
            "The grader is busy. Try again in a minute.",
        )
    }
}

async fn grade_route(
    State(explorer): State<Arc<CompilerExplorer>>,
    body: Result<Json<GradeRequest>, JsonRejection>,
) -> Response {
    let request = match body {
        Ok(Json(request)) => request,
        Err(rejection) => {
            let status = rejection.status();
            tracing::info!(status = status.as_u16(), "unreadable grade request");
            return error(status, &rejection.body_text());
        }
    };
    if let Some(problem) = request.problem() {
        tracing::info!(%problem, "grade request refused");
        return error(StatusCode::BAD_REQUEST, &problem);
    }

    let started = Instant::now();
    let result = grade(&explorer, &request).await;
    let elapsed_ms = u64::try_from(started.elapsed().as_millis()).unwrap_or(u64::MAX);
    match result {
        Ok(response) => {
            let count = |verdict| {
                response
                    .tests
                    .iter()
                    .filter(|test| test.verdict == verdict)
                    .count()
            };
            // The source, the input, and the output are never logged.
            tracing::info!(
                compiler = %request.compiler,
                language = %request.language,
                tests = request.tests.len(),
                compiled = response.compiled,
                pass = count(Verdict::Pass),
                wrong_answer = count(Verdict::WrongAnswer),
                runtime_error = count(Verdict::RuntimeError),
                time_limit = count(Verdict::TimeLimit),
                elapsed_ms,
                "graded"
            );
            Json(response).into_response()
        }
        Err(failure) => {
            tracing::warn!(?failure, elapsed_ms, "compiler explorer gave no run");
            let status = if failure == UpstreamError::Busy {
                StatusCode::SERVICE_UNAVAILABLE
            } else {
                StatusCode::BAD_GATEWAY
            };
            error(status, &failure.message())
        }
    }
}
