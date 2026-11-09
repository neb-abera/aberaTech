# Grader

The drill grader for abera.tech's /training. The site's .NET server posts a
drill to `POST /grade`. The grader sends each test to
[Compiler Explorer](https://godbolt.org)'s public API, which compiles and
runs the code, and compares the output with the test. No submitted code
runs in this process. The browser never calls it. In production it has
internal ingress only, so nothing outside the Container Apps environment
reaches it.

Generated from [modern-rust-template](https://github.com/neb-abera/modern-rust-template)
and kept to its gates. It lives in the aberaTech repository, so the
deploy workflow that ships the site ships the grader, under the same OIDC
identity and registry, and a change to the API and its .NET caller lands in
one pull request.

## API

`POST /grade`, JSON, at most 256 KiB:

```json
{
  "source": "#include <cstdio>\nint main(){int a,b;scanf(\"%d %d\",&a,&b);printf(\"%d\\n\",a+b);}",
  "language": "c++",
  "compiler": "g162",
  "flags": "-O2 -std=c++23",
  "tests": [{ "stdin": "2 3\n", "expected": "5\n", "timeLimitMs": 2000 }]
}
```

| Field         | Rule                                                               |
| ------------- | ------------------------------------------------------------------ |
| `source`      | Not blank, at most 16 KiB                                          |
| `language`    | `c++`, `c`, or `rust`, as Compiler Explorer names them             |
| `compiler`    | A Compiler Explorer id: 1 to 40 letters, digits, `.`, `-`, or `_`  |
| `flags`       | Optional, at most 200 printable ASCII characters                   |
| `tests`       | 1 to 10                                                            |
| `stdin`       | At most 4 KiB                                                      |
| `expected`    | At most 4 KiB                                                      |
| `timeLimitMs` | 1 to 10000                                                         |

The answer:

```json
{
  "compiled": true,
  "compileError": null,
  "tests": [
    { "verdict": "pass", "stdout": "5", "message": null, "diff": null, "execMs": 3 }
  ]
}
```

A verdict is one of these:

| Verdict         | When                                                                          |
| --------------- | ----------------------------------------------------------------------------- |
| `pass`          | The output matches                                                            |
| `wrong_answer`  | It ran in time and printed something else. `diff` names the first line        |
| `compile_error` | It did not build. `message` and `compileError` hold the compiler's errors     |
| `runtime_error` | It exited other than 0 or did not start. `message` has the code and stderr    |
| `time_limit`    | Compiler Explorer stopped it, or `execMs` is over `timeLimitMs`                |

Outputs match when their lines match after trailing ASCII whitespace is
dropped from each line and blank lines are dropped from the end. A carriage
return counts as whitespace. `src/outputs.rs` holds the rule, and Kani
proves it equal to a second statement of it for every 3-byte input.

Errors are JSON `{"error": "..."}`: 400 for a refused request, 413 over the
size limit, 415 without a JSON content type, 422 for an unknown field, 503
when Compiler Explorer stays busy or the grader is at capacity, 502 when
Compiler Explorer fails, and 504 past 90 seconds.

`GET /healthz` answers `ok`. `grader probe` checks it from inside the image,
which has no shell.

## Limits

- 8 gradings at once. The next is refused with 503 at once.
- 4 calls to Compiler Explorer at once, across every grading.
- A 429 is waited out as Retry-After says, or 2 then 4 seconds, 3 tries at
  most. A wait over 10 seconds is not waited.
- 30 seconds a call, 5 to connect, 90 seconds a grading. An answer over 1
  MiB is refused.

## Settings

| Variable                      | Default               |
| ----------------------------- | --------------------- |
| `GRADER_ADDR`                 | `0.0.0.0:8080`        |
| `GRADER_COMPILER_EXPLORER`    | `https://godbolt.org` |
| `GRADER_UPSTREAM_CONCURRENCY` | `4`                   |
| `GRADER_LOG`                  | `info`                |

Logs are JSON lines on stdout. A grading logs the compiler, the language,
the test count, the count of each verdict, and the time. The source, the
input, and the output are never logged.

## Development

From the repository root:

```bash
make grader-verify   # the verification suite, in the toolchain image
make grader-fuzz     # 60 seconds of fuzzing
make e2e             # the site, the grader, and a fake Compiler Explorer
```

`scripts/verify.sh` lists its checks at the top. It runs the template's
suite less the checks that cover the whole repository, which run at the
root, the package purity check, since the crate is never published, and the
macOS and Windows builds, since the grader runs on Linux only.

`tests/common/fake.rs` is Compiler Explorer in memory, for the tests and
for `make e2e`, where it runs as its own image stage. It answers by comments
in the source, the same way the server's `FakeCompilerExplorer.cs` does.

## Security

- Submitted code runs on Compiler Explorer. This process only reads its output.
- Internal ingress only. The site's server is its one caller, and the site
  checks the owner's sign-in and rate limit before it calls.
- No secrets. Compiler Explorer's public API takes none.
- The compiler id goes into a URL path, so it is checked against a strict
  pattern before any call.
- Distroless image, running as `nonroot`.
