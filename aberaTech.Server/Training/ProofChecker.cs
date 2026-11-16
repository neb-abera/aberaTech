using System.Text;

namespace aberaTech.Server.Training;

/// <summary>A Lean proof to check: the owner's file, and the statement <c>answer</c> must prove.</summary>
public sealed record ProofRun(string Source, string Statement);

/// <summary>
/// What the check found. <c>Outcome</c> is the grader's: <c>proved</c>,
/// <c>error</c>, <c>sorry</c>, <c>axiom</c>, <c>statement</c>, or
/// <c>forbidden</c>. This server adds <c>unavailable</c> when it has no
/// grader to ask.
/// </summary>
public sealed record ProofResult(string Outcome, string? Message, string Output);

/// <summary>
/// Checks a Lean proof through the Rust grader's <c>POST /prove</c>
/// (grader/src/prove.rs), which sends it to Compiler Explorer's Lean. The
/// check lives in the grader alone, so this server has no in-process path
/// for proofs: with no <c>Training:GraderUrl</c> it answers
/// <c>unavailable</c>. Under <c>make e2e</c> the grader runs against the
/// fake Compiler Explorer (grader/tests/common/fake.rs), as for drills.
/// </summary>
public sealed class ProofChecker(GraderClient grader)
{
    public const int MaxSourceBytes = 16 * 1024;
    public const int MaxStatementBytes = 1024;

    public static string? Invalid(ProofRun run)
    {
        if (run.Source is null || string.IsNullOrWhiteSpace(run.Source)) return "The proof is empty.";
        if (Encoding.UTF8.GetByteCount(run.Source) > MaxSourceBytes) return $"The proof is over {MaxSourceBytes / 1024} KB.";
        if (run.Statement is null || string.IsNullOrWhiteSpace(run.Statement)) return "The statement is empty.";
        if (Encoding.UTF8.GetByteCount(run.Statement) > MaxStatementBytes) return $"The statement is over {MaxStatementBytes} bytes.";
        if (run.Statement.Contains('\n') || run.Statement.Contains('\r') || run.Statement.Contains(":=", StringComparison.Ordinal))
            return "The statement is one line with no :=.";
        return null;
    }

    public async Task<ProofResult> CheckAsync(ProofRun run, CancellationToken cancellationToken)
    {
        if (grader.IsConfigured) return await grader.ProveAsync(run, cancellationToken);
        return new ProofResult("unavailable", "Lean proofs are checked by the grader, and this server has no Training:GraderUrl.", "");
    }
}
