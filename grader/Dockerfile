# The grader's images, from modern-rust-template's Dockerfile.
#
#   docker build grader                                  the production image (the last stage)
#   docker build --target toolchain grader               every tool the gates use (make grader-verify)
#   docker build --target fake-compiler-explorer grader  the end-to-end suite's fake upstream
#
# Every FROM line is a literal image pinned by digest, so Dependabot sees and
# bumps each one (.github/dependabot.yml, directory /grader).

# GCC, the newest release, for the C++ half of libfuzzer-sys. The base
# image's Debian ships GCC 14.
FROM gcc:16.2.0@sha256:17043c23ec2ad97fc3beca18da77275d26be5db5cd70ea0a5d73200e21a8e999 AS gcc
RUN rm -rf /usr/local/bin/*go* /usr/local/bin/*gfortran* /usr/local/lib/go \
        /usr/local/lib64/libgo* /usr/local/lib64/libgfortran* \
        /usr/local/libexec/gcc/*/*/go1 /usr/local/libexec/gcc/*/*/f951 \
        /usr/local/libexec/gcc/*/*/cgo /usr/local/libexec/gcc/*/*/vet \
        /usr/local/libexec/gcc/*/*/buildid /usr/local/libexec/gcc/*/*/test2json

# The pinned stable compiler. Its tag must match the channel in
# rust-toolchain.toml and the rust-version in Cargo.toml.
# scripts/check-toolchain.sh enforces the pairing.
FROM rust:1.99.0-slim@sha256:2752b332db73fdbb7dc576f06c82ed1f312005784ef913d7e04a28f5f55dc581 AS rust

# The release build: the production binary and the end-to-end fake.
FROM rust AS build
WORKDIR /src
COPY --chown=root:root . .
RUN --mount=type=cache,target=/usr/local/cargo/registry \
    cargo build --release --locked --bin grader --example fake_compiler_explorer

# The end-to-end suite's Compiler Explorer (tests/common/fake.rs). It runs
# nothing, and the production stage below never copies it.
FROM gcr.io/distroless/cc-debian13:nonroot@sha256:cc66d222f0a7ec996b78a99fe76a969bc80b539a5016ad866c14d1949bc6d0ab AS fake-compiler-explorer
COPY --from=build /src/target/release/examples/fake_compiler_explorer /fake-compiler-explorer
USER nonroot
EXPOSE 10240
ENTRYPOINT ["/fake-compiler-explorer"]

# The toolchain image: every tool scripts/verify.sh uses, pinned.
FROM rust AS toolchain

# The pinned nightly, used only where stable cannot go: Miri and cargo-fuzz.
# Scripts and CI derive the value from this line.
ENV NIGHTLY_TOOLCHAIN=nightly-2026-09-27

# The pinned Kani version, for the proof gate.
ENV KANI_VERSION=0.68.0

# The pinned cargo-vet version. The audit format is versioned in
# supply-chain/config.toml, so the tool and the files have to agree.
ENV CARGO_VET_VERSION=0.10.2

RUN apt-get update && apt-get upgrade -y && \
    apt-get install -y --no-install-recommends git curl ca-certificates && \
    rm -rf /var/lib/apt/lists/*

# GCC 16 in /usr/local, ahead of Debian's GCC 14 on PATH and in the loader
# cache. rustc links through cc and the cc crate compiles libfuzzer-sys
# through c++, so one GCC builds and links the C++.
COPY --from=gcc /usr/local/ /usr/local/
RUN ln -s gcc /usr/local/bin/cc && \
    echo /usr/local/lib64 > /etc/ld.so.conf.d/000-gcc.conf && ldconfig

# A non-root user. The base image leaves RUSTUP_HOME and CARGO_HOME
# world-writable so toolchains and tools install without root.
RUN useradd --create-home --uid 1000 dev && chmod 755 /home/dev
USER dev
WORKDIR /home/dev

RUN rustup component add clippy rustfmt llvm-tools

# The nightly with Miri. Miri builds its sysroot on first use.
RUN rustup toolchain install "$NIGHTLY_TOOLCHAIN" --profile minimal \
        --component miri --component rust-src

#   cargo-deny     supply-chain gate: advisories, licenses, bans, sources
#   cargo-llvm-cov code coverage
#   cargo-fuzz     libFuzzer front end
#   cargo-vet      supply-chain gate: has anyone read this dependency
RUN cargo install --locked cargo-binstall && \
    cargo binstall -y cargo-deny cargo-llvm-cov cargo-fuzz \
        "cargo-vet@$CARGO_VET_VERSION" && \
    rm -rf "$CARGO_HOME/registry" "$CARGO_HOME/git"

# Kani, the model checker the proof gate runs, with its pinned bundle.
RUN cargo install --locked "kani-verifier@$KANI_VERSION" && \
    cargo kani setup && \
    rm -rf "$CARGO_HOME/registry" "$CARGO_HOME/git"

# The production image: the binary on distroless, as nonroot (65532). No
# shell and no package manager. `grader probe` is the health check.
FROM gcr.io/distroless/cc-debian13:nonroot@sha256:cc66d222f0a7ec996b78a99fe76a969bc80b539a5016ad866c14d1949bc6d0ab AS runtime
COPY --from=build /src/target/release/grader /grader
USER nonroot
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=3s --retries=3 CMD ["/grader", "probe"]
ENTRYPOINT ["/grader"]
