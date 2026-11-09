//! Fuzz target: every byte string a caller or Compiler Explorer can send.
//! A request body is read or refused without a panic, and two outputs
//! match exactly when no line differs.

#![no_main]

use grader::outputs::{first_difference, outputs_match};
use grader::request::GradeRequest;
use libfuzzer_sys::fuzz_target;

fuzz_target!(|data: &[u8]| {
    if let Ok(request) = serde_json::from_slice::<GradeRequest>(data) {
        let _ = request.problem();
    }
    let text = String::from_utf8_lossy(data);
    let mut middle = text.len() / 2;
    while !text.is_char_boundary(middle) {
        middle -= 1;
    }
    let (expected, actual) = text.split_at(middle);
    assert_eq!(
        outputs_match(expected, actual),
        first_difference(expected, actual).is_none()
    );
});
