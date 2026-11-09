//! The drill grader for abera.tech's /training.
//!
//! The site's .NET server posts a drill's source and tests to `POST /grade`.
//! The grader sends each test to Compiler Explorer's public API, which
//! compiles and runs the code, and compares what came back with the test.
//! No submitted code runs in this process. The browser never calls it: the
//! service takes internal traffic only.

pub mod app;
pub mod grade;
pub mod outputs;
pub mod probe;
pub mod request;
pub mod upstream;
