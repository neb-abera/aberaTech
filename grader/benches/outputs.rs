//! Criterion benchmark of output matching on the largest output a test allows.

#![allow(missing_docs)]

use std::hint::black_box;

use criterion::{Criterion, criterion_group, criterion_main};
use grader::outputs::{first_difference, outputs_match};

fn matching(c: &mut Criterion) {
    let expected = (0..800)
        .map(|i| i.to_string())
        .collect::<Vec<_>>()
        .join("\n");
    let actual = expected.replace('\n', "  \r\n");
    c.bench_function("outputs_match 4 KiB", |b| {
        b.iter(|| outputs_match(black_box(&expected), black_box(&actual)));
    });
    c.bench_function("first_difference 4 KiB", |b| {
        b.iter(|| first_difference(black_box(&expected), black_box(&actual)));
    });
}

criterion_group!(benches, matching);
criterion_main!(benches);
