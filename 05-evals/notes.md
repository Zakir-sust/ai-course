# 05-evals — notes

C06: an LLM judge vs my own labels. Skeleton by Claude; the scores, the "why" and the disagreement line are mine.
The 5 answers are in `answers.json` (run 1, the C04 pipeline + a royalties question). Read each answer next to its `Sources`.

Scale, the same one the judge uses (1–5):
- **Relevance:** does the answer address the question? 5 = fully, 1 = off-topic.
- **Groundedness:** is every claim backed by the 5 sources? 5 = all of it, 1 = made up.

**My columns go in before run 2.** Then the judge's columns from run 2's table.
Judge = `qwen3.5:9b`, run 12 Nov (2 min). "?" = the evaluator couldn't parse a score from the judge's reply. A second run failed on different rows (#2 relevance too).

| # | Question | Me R | Me G | Judge R | Judge G | Why (mine, a few words) |
|---|---|------|------|---|---|---|
| 1 | Max withholding rate on dividends | 5 | 5 | ? (parse failed) | 5 |  |
| 2 | How is interest taxed | 4 | 5 | 4 | 5 |  |
| 3 | What counts as a permanent establishment | 5 | 5 | 4 | 5 |  |
| 4 | Corporate income tax rate in Bangladesh | 5 | 5 | ? (parse failed) | ? (parse failed) |  |
| 5 | Max withholding rate on royalties | 5 | 5 | 4 | 5 |  |

**Where the judge and I disagree, and why:** (mine, rephrased by Claude) Groundedness: we agree on every row it scored. Relevance: I gave 5s, the judge never does. Its 5 means "complete plus extra insight", and it doesn't see the sources, so it compares the answer with its own knowledge. 3 of 10 scores didn't parse (the 9B judge reasons too long or loses the score format, and the refusal in #4 doesn't fit the rubric), and a second run failed on different rows.

**Why groundedness gets the sources and relevance doesn't** (I didn't know; Claude's answer): groundedness asks "is every claim in these sources?", so the judge needs them to compare against. Relevance asks "does it answer the question?", which only needs the question and the answer.

## What I learned (3 lines)
- We judge an answer on relevance and groundedness. Relevance = how closely and completely the answer addresses the question. Groundedness = whether the answer is based on the sources. An answer can be wrong overall and still fully grounded if it follows the sources.
- `EvaluateAsync` returns an `EvaluationResult`; each score is a `NumericMetric` I read by name (`Value`, `Reason`, `Diagnostics`).
- (Claude, from the run) A small local judge is unreliable: scores fail to parse at random and its scale differs from mine, so a judge has to be checked against human labels before I trust it (C24).
