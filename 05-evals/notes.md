# 05-evals — notes

C06: an LLM judge vs my own labels. Skeleton by Claude; the scores, the "why" and the disagreement line are mine.
Data swapped on 13 Nov from the US–Bangladesh tax treaty to Apple's fiscal 2026 annual report (10-K). Claude re-ran
**run 1 only**: the 5 new answers are in `answers.json` (the 03-rag pipeline + a net-income question). The judge has
**not** been run on them, so my scores still go in first, blind. Read each answer next to its `Sources` (and the PDF,
`findoc/sources/apple-fy2026.pdf`: total net sales, R&D and net income are on PDF page 35).

Scale, the same one the judge uses (1–5):
- **Relevance:** does the answer address the question? 5 = fully, 1 = off-topic.
- **Groundedness:** is every claim backed by the 5 sources? 5 = all of it, 1 = made up.

**My columns went in before run 2** (14 Nov), then the judge's columns from run 2 (`dotnet run`, ~2 min). – = the judge's score didn't parse.

| # | Question | Me R | Me G | Judge R | Judge G | Why (mine, a few words) |
|---|---|------|------|---|---|---|
| 1 | Total net sales in fiscal 2026 | 4 | 5 | 4 | 5 |  |
| 2 | R&D spending | 4 | 1 | 5 | – |  |
| 3 | Where is the company headquartered | 4 | 5 | 4 | 5 |  |
| 4 | Microsoft's revenue in fiscal 2026 (not in the report) | 5 | 5 | 4 | – | not in the sources, and the model said so |
| 5 | Net income in fiscal 2026 | 1 | 1 | – | 2 | generation failure: $132,175M is in source [5], the model still refused |

**Where the judge and I disagree, and why:** #4 (Microsoft): the revenue isn't in the sources and the model said so correctly, so I gave it 5. The judge gave relevance 4, and its groundedness score didn't parse.

**Rerun with the unparsed summary (14 Nov, Claude, from the run):** 5 of 10 scores didn't parse (I predicted 3), all on the hard rows: #2 both, #4 both, #5 relevance. #1 and #3 parsed in both runs. Every failed reply holds only the `<S0>` reasoning and stops mid-sentence after ~2,500–3,500 characters, before the score tag: it looks like the reply hit an output-length cap. The hard rows make the judge reason longer, so they're the ones that lose their score. → C23.

**Why groundedness gets the sources and relevance doesn't** (I didn't know; Claude's answer): groundedness asks "is every claim in these sources?", so the judge needs them to compare against. Relevance asks "does it answer the question?", which only needs the question and the answer.

## From the first run (12 Nov, tax-treaty answers)
- Groundedness: the judge agreed with me on every row it scored. Relevance: I gave 5s, the judge gave 4s (its 5 means
  "complete plus extra insight", and it doesn't see the sources, so it compares the answer with its own knowledge).
- 3 of 10 scores didn't parse (the 9B judge reasons too long or loses the score format; a refusal doesn't fit the rubric),
  and a second run failed on different rows. Expect the same here.

## What I learned (3 lines)
- We judge an answer on relevance and groundedness. Relevance = how closely and completely the answer addresses the question. Groundedness = whether the answer is based on the sources. An answer can be wrong overall and still fully grounded if it follows the sources.
- `EvaluateAsync` returns an `EvaluationResult`; each score is a `NumericMetric` I read by name (`Value`, `Reason`, `Diagnostics`).
- (Claude, from the run) A small local judge is unreliable: scores fail to parse at random and its scale differs from mine, so a judge has to be checked against human labels before I trust it (C24).
