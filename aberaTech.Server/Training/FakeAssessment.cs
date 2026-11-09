using System.Text.Json;

namespace aberaTech.Server.Training;

/// <summary>
/// Sample questions for `make e2e` (Training:Fake, Development only). The
/// syllabus has no questions until the content is written, so the owner
/// suite gets these through /api/training/solves and the page adds them to
/// the C++ refresher: a choice and a number on its last lesson, both held
/// for the unit's mastery check. Production never registers this class, so
/// the answer there carries no <c>fixture</c>.
/// </summary>
public sealed class FakeAssessment
{
    public JsonElement Fixture { get; } = JsonDocument.Parse(
        """
        {
          "lessons": {
            "refresh-cpp-lambdas": [
              {
                "id": "fixture-overflow",
                "type": "choice",
                "prompt": "Two ints near 2e9 are added. What prints?",
                "choices": ["Their sum", "Undefined behavior, often a negative number", "A compile error"],
                "answer": 1,
                "why": "Signed overflow is undefined. Use long long for sums that pass 2^31 - 1.",
                "ref": { "label": "USACO Guide: Debugging checklist", "url": "https://usaco.guide/general/debugging-checklist" }
              },
              {
                "id": "fixture-power",
                "type": "number",
                "prompt": "How many bits does an unsigned 64-bit value hold? Give 2 to that power as an expression.",
                "answer": "2^64",
                "why": "64 bits give 2^64 values.",
                "ref": { "label": "USACO Guide: Data types", "url": "https://usaco.guide/general/data-types" }
              }
            ]
          },
          "units": {
            "refresh-cpp": { "mastery": ["fixture-overflow", "fixture-power"] }
          }
        }
        """).RootElement.Clone();
}
