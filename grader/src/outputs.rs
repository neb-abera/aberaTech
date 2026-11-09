//! What counts as the same output.
//!
//! Judges compare lines. Trailing whitespace on a line does not count, and
//! neither do blank lines at the end. A carriage return is whitespace, so
//! `"5\r\n"` matches `"5\n"`. Whitespace here is ASCII: space, tab, carriage
//! return, line feed, vertical tab, and form feed. The .NET path
//! (`DrillRunner.Normalize`) trims the same way for ASCII output.

use serde::Serialize;

/// Whether `byte` is whitespace a judge ignores at the end of a line.
#[must_use]
pub const fn is_space(byte: u8) -> bool {
    matches!(byte, b' ' | b'\t' | b'\r' | b'\n' | 0x0b | 0x0c)
}

/// `bytes` without its trailing whitespace.
#[must_use]
pub fn trim_end(bytes: &[u8]) -> &[u8] {
    let mut end = bytes.len();
    while end > 0 && is_space(bytes[end - 1]) {
        end -= 1;
    }
    &bytes[..end]
}

/// The lines a judge compares: everything after the last visible byte
/// dropped, then each line trimmed at its end.
pub fn significant_lines(text: &[u8]) -> impl Iterator<Item = &[u8]> {
    trim_end(text).split(|&byte| byte == b'\n').map(trim_end)
}

/// Whether two outputs are the same to a judge.
#[must_use]
pub fn bytes_match(expected: &[u8], actual: &[u8]) -> bool {
    significant_lines(expected).eq(significant_lines(actual))
}

/// Whether two outputs are the same to a judge.
///
/// ```
/// assert!(grader::outputs::outputs_match("5\n", "5  \r\n\n"));
/// assert!(!grader::outputs::outputs_match("5\n", "6\n"));
/// ```
#[must_use]
pub fn outputs_match(expected: &str, actual: &str) -> bool {
    bytes_match(expected.as_bytes(), actual.as_bytes())
}

/// The output as a judge reads it, lines joined with `\n`.
#[must_use]
pub fn normalize(text: &str) -> String {
    let lines: Vec<&[u8]> = significant_lines(text.as_bytes()).collect();
    // Trimming ASCII bytes off valid UTF-8 leaves valid UTF-8.
    String::from_utf8_lossy(&lines.join(&b'\n')).into_owned()
}

/// The first line where two outputs differ. A missing line is `None`.
#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
pub struct Difference {
    /// The line number, from 1.
    pub line: usize,
    /// The expected line, or `None` when the expected output ended first.
    pub expected: Option<String>,
    /// The line the program printed, or `None` when it stopped first.
    pub actual: Option<String>,
}

/// The first line where `actual` differs from `expected`, or `None` when
/// they match.
///
/// ```
/// let difference = grader::outputs::first_difference("1\n2\n", "1\n3\n").unwrap();
/// assert_eq!(difference.line, 2);
/// assert_eq!(difference.expected.as_deref(), Some("2"));
/// assert_eq!(difference.actual.as_deref(), Some("3"));
/// ```
#[must_use]
pub fn first_difference(expected: &str, actual: &str) -> Option<Difference> {
    let mut want = significant_lines(expected.as_bytes());
    let mut got = significant_lines(actual.as_bytes());
    let mut line = 0;
    loop {
        line += 1;
        let (left, right) = (want.next(), got.next());
        if left.is_none() && right.is_none() {
            return None;
        }
        if left != right {
            let text = |bytes: &[u8]| String::from_utf8_lossy(bytes).into_owned();
            return Some(Difference {
                line,
                expected: left.map(text),
                actual: right.map(text),
            });
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn trailing_spaces_and_blank_lines_do_not_count() {
        assert!(outputs_match("0\n", "0  \n\n"));
        assert!(outputs_match("1 2\n3", "1 2\t\r\n3\r\n"));
        assert!(outputs_match("", "\n\n  \n"));
    }

    #[test]
    fn a_space_inside_a_line_counts() {
        assert!(!outputs_match("1 2", "1  2"));
        assert!(!outputs_match("1\n2", "1\n\n2"));
        assert!(!outputs_match(" 1", "1"));
    }

    #[test]
    fn normalize_trims_each_line_and_the_end() {
        assert_eq!(normalize("a \r\nb\t\n\n \n"), "a\nb");
        assert_eq!(normalize(""), "");
        assert_eq!(normalize("é \n"), "é");
    }

    #[test]
    fn the_first_difference_names_the_line() {
        assert_eq!(first_difference("5\n", "5 \n"), None);
        assert_eq!(
            first_difference("1\n2\n", "1\n"),
            Some(Difference {
                line: 2,
                expected: Some("2".into()),
                actual: None
            })
        );
        assert_eq!(
            first_difference("1", "1\n9"),
            Some(Difference {
                line: 2,
                expected: None,
                actual: Some("9".into())
            })
        );
    }

    #[test]
    fn every_ascii_whitespace_byte_is_space_and_nothing_else_is() {
        let spaces: Vec<u8> = (0..=u8::MAX).filter(|&byte| is_space(byte)).collect();
        assert_eq!(spaces, vec![b'\t', b'\n', 0x0b, 0x0c, b'\r', b' ']);
    }
}

/// Proofs for every input of three and four bytes, checked by Kani.
#[cfg(kani)]
mod proofs {
    use super::*;

    /// The specification, written another way and without allocation: copy
    /// each line with its trailing whitespace dropped, keep the line breaks,
    /// then drop the line breaks at the end.
    fn spec(text: &[u8; 3]) -> ([u8; 3], usize) {
        let mut out = [0_u8; 3];
        let (mut len, mut line_start) = (0, 0);
        let mut index = 0;
        while index <= text.len() {
            if index == text.len() || text[index] == b'\n' {
                let mut end = index;
                while end > line_start && is_space(text[end - 1]) {
                    end -= 1;
                }
                let mut copy = line_start;
                while copy < end {
                    out[len] = text[copy];
                    len += 1;
                    copy += 1;
                }
                if index < text.len() {
                    out[len] = b'\n';
                    len += 1;
                }
                line_start = index + 1;
            }
            index += 1;
        }
        while len > 0 && out[len - 1] == b'\n' {
            len -= 1;
        }
        (out, len)
    }

    fn same(a: &[u8; 3], b: &[u8; 3]) -> bool {
        let ((left, left_len), (right, right_len)) = (spec(a), spec(b));
        left_len == right_len && left[..left_len] == right[..right_len]
    }

    #[kani::proof]
    #[kani::unwind(6)]
    fn matching_is_reflexive_and_symmetric() {
        let a: [u8; 3] = kani::any();
        let b: [u8; 3] = kani::any();
        assert!(bytes_match(&a, &a));
        assert_eq!(bytes_match(&a, &b), bytes_match(&b, &a));
    }

    #[kani::proof]
    #[kani::unwind(6)]
    fn matching_agrees_with_the_specification() {
        let a: [u8; 3] = kani::any();
        let b: [u8; 3] = kani::any();
        assert_eq!(bytes_match(&a, &b), same(&a, &b));
    }

    #[kani::proof]
    #[kani::unwind(6)]
    fn trailing_whitespace_never_changes_a_verdict() {
        let a: [u8; 3] = kani::any();
        let space: u8 = kani::any();
        kani::assume(is_space(space));
        let longer = [a[0], a[1], a[2], space];
        assert!(bytes_match(&a, &longer));
    }
}
