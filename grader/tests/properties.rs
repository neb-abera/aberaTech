//! Properties of output matching and request checks, for generated inputs.

#![cfg(not(miri))]
#![allow(missing_docs)]

use grader::outputs::{first_difference, normalize, outputs_match};
use grader::request::GradeRequest;
use proptest::prelude::*;

/// Output made of the characters that matter to a judge.
fn output() -> impl Strategy<Value = String> {
    "[ab1 \t\r\n]{0,24}"
}

fn whitespace() -> impl Strategy<Value = String> {
    "[ \t\r\n]{0,6}"
}

proptest! {
    #[test]
    fn normalizing_twice_changes_nothing(text in output()) {
        let once = normalize(&text);
        prop_assert_eq!(normalize(&once), once);
    }

    #[test]
    fn an_output_matches_its_normal_form(text in output()) {
        prop_assert!(outputs_match(&text, &normalize(&text)));
    }

    #[test]
    fn trailing_whitespace_never_changes_a_verdict(a in output(), b in output(), tail in whitespace()) {
        let longer = format!("{b}{tail}");
        prop_assert_eq!(outputs_match(&a, &b), outputs_match(&a, &longer));
    }

    #[test]
    fn matching_is_symmetric_and_equals_equal_normal_forms(a in output(), b in output()) {
        prop_assert_eq!(outputs_match(&a, &b), outputs_match(&b, &a));
        prop_assert_eq!(outputs_match(&a, &b), normalize(&a) == normalize(&b));
    }

    #[test]
    fn a_difference_exists_exactly_when_outputs_differ(a in output(), b in output()) {
        prop_assert_eq!(first_difference(&a, &b).is_none(), outputs_match(&a, &b));
    }

    #[test]
    fn any_body_is_read_or_refused_without_a_panic(body in ".{0,200}") {
        if let Ok(request) = serde_json::from_str::<GradeRequest>(&body) {
            let _ = request.problem();
        }
    }
}
