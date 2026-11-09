//! The grader's entry point.
//!
//! Settings come from the environment:
//!
//! - `GRADER_ADDR`: where to listen, `0.0.0.0:8080` by default.
//! - `GRADER_COMPILER_EXPLORER`: Compiler Explorer's address,
//!   `https://godbolt.org` by default. The end-to-end suite points it at a fake.
//! - `GRADER_UPSTREAM_CONCURRENCY`: calls to Compiler Explorer in flight at once, 4 by default.
//! - `GRADER_LOG`: a tracing filter, `info` by default.
//!
//! `grader probe` checks `/healthz` on the listening port and exits 0 when it answers 200.

use std::net::SocketAddr;
use std::process::ExitCode;

use grader::app::{Limits, router};
use grader::upstream::{CompilerExplorer, Settings};
use tracing_subscriber::EnvFilter;

fn address() -> Result<SocketAddr, String> {
    let text = std::env::var("GRADER_ADDR").unwrap_or_else(|_| "0.0.0.0:8080".into());
    text.parse()
        .map_err(|_| format!("GRADER_ADDR is not an address: {text}"))
}

fn settings() -> Result<Settings, String> {
    let mut settings = Settings::default();
    if let Ok(base) = std::env::var("GRADER_COMPILER_EXPLORER") {
        if !(base.starts_with("https://") || base.starts_with("http://")) {
            return Err(format!("GRADER_COMPILER_EXPLORER is not a URL: {base}"));
        }
        base.trim_end_matches('/').clone_into(&mut settings.base);
    }
    if let Ok(text) = std::env::var("GRADER_UPSTREAM_CONCURRENCY") {
        settings.concurrency = text
            .parse()
            .ok()
            .filter(|&count| (1..=64).contains(&count))
            .ok_or_else(|| format!("GRADER_UPSTREAM_CONCURRENCY is 1 to 64, not {text}"))?;
    }
    Ok(settings)
}

/// Resolves on SIGTERM, which Container Apps and compose send, or Ctrl-C.
async fn shutdown() {
    let terminate = async {
        match tokio::signal::unix::signal(tokio::signal::unix::SignalKind::terminate()) {
            Ok(mut signal) => {
                signal.recv().await;
            }
            Err(_) => std::future::pending::<()>().await,
        }
    };
    tokio::select! {
        _ = tokio::signal::ctrl_c() => {}
        () = terminate => {}
    }
    tracing::info!("shutting down");
}

#[tokio::main]
async fn main() -> ExitCode {
    let address = match address() {
        Ok(address) => address,
        Err(problem) => {
            eprintln!("{problem}");
            return ExitCode::FAILURE;
        }
    };
    if std::env::args().nth(1).as_deref() == Some("probe") {
        let local = SocketAddr::from(([127, 0, 0, 1], address.port()));
        return if grader::probe::healthy(local) {
            ExitCode::SUCCESS
        } else {
            ExitCode::FAILURE
        };
    }

    tracing_subscriber::fmt()
        .json()
        .with_current_span(false)
        .with_env_filter(
            EnvFilter::try_from_env("GRADER_LOG").unwrap_or_else(|_| EnvFilter::new("info")),
        )
        .init();

    let explorer = match settings().map(CompilerExplorer::new) {
        Ok(Ok(explorer)) => explorer,
        Ok(Err(failure)) => {
            tracing::error!(%failure, "the HTTP client did not build");
            return ExitCode::FAILURE;
        }
        Err(problem) => {
            tracing::error!(%problem, "bad setting");
            return ExitCode::FAILURE;
        }
    };
    let listener = match tokio::net::TcpListener::bind(address).await {
        Ok(listener) => listener,
        Err(failure) => {
            tracing::error!(%failure, %address, "could not listen");
            return ExitCode::FAILURE;
        }
    };
    tracing::info!(%address, "grader listening");
    let served = axum::serve(listener, router(explorer, Limits::default()))
        .with_graceful_shutdown(shutdown())
        .await;
    if let Err(failure) = served {
        tracing::error!(%failure, "server stopped");
        return ExitCode::FAILURE;
    }
    ExitCode::SUCCESS
}
