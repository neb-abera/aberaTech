//! Compiler Explorer in memory, for the end-to-end suite (compose.yaml).
//! It runs nothing. `tests/common/fake.rs` says what it answers. Listens on
//! `FAKE_ADDR`, `0.0.0.0:10240` by default. Never in the production image.

#![allow(clippy::unwrap_used)]

#[path = "../tests/common/fake.rs"]
mod fake;

use std::sync::Arc;

#[tokio::main]
async fn main() {
    let address = std::env::var("FAKE_ADDR").unwrap_or_else(|_| "0.0.0.0:10240".into());
    let listener = tokio::net::TcpListener::bind(&address).await.unwrap();
    println!("fake compiler explorer listening on {address}");
    axum::serve(listener, fake::router(Arc::new(fake::Fake::default())))
        .await
        .unwrap();
}
