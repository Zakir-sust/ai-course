# 02-embeddings — notes

Written by Claude at my request, from my code and its output. Data swapped from tax-treaty sentences to annual-report
(10-K) sentences on 13 Nov, re-run by Claude; my code (TODOs 1–4) is unchanged. The treaty version is in git history.

- **Built:** a console app that embeds 20 annual-report-style sentences (revenue, expenses, people, risks) with
  `qwen3-embedding:0.6b`, embeds each query, scores it against every sentence with cosine similarity, and prints the top 3.
- **Vector dimension:** 1024.
- **Why cosine:** cosine similarity = dot product ÷ (length of A × length of B). It measures the angle between two vectors,
  so it compares direction (meaning) and ignores vector length. A higher score means the sentences are closer in meaning.
  It's a ranking signal, not a percentage of correctness.
- **Result:** 5 of 5 queries hit top-1. No query uses the words of its target sentence ("How many people work there?"
  finds "…approximately 160,000 full-time equivalent employees").
- **What surprised me:** the margins are thin. "How many people work there?" won by only 0.016 (0.499 vs 0.483), and all
  three of its top 3 are people sentences: the topic is easy, the exact sentence is not.
- **Close call:** "Why are the costs of hiring AI experts going up?" → [12] (0.639), but the R&D-for-AI sentence [5] came
  second at 0.603. Shared words ("AI", "costs") pull in a neighbour from another topic.
- **Two questions in one:** "Does subscription income for software count, and when is it booked?" still hit [1] (0.528),
  with the revenue-recognition sentence [3] second (0.431). With treaties, a query like this missed; here it didn't. The
  risk is the same: one vector has to stand for two questions.
- **Learn-back:** doubling every number in one vector leaves the cosine score unchanged. The dot product doubles and that
  vector's length doubles, so the two cancel. Only the direction counts.
