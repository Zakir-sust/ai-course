# 02-embeddings — notes

Written by Claude at my request, from my code and its output.

- **Built:** a console app that embeds 20 treaty-style sentences with `qwen3-embedding:0.6b`, embeds each query, scores it against every sentence with cosine similarity, and prints the top 3.
- **Vector dimension:** 1024.
- **Why cosine:** cosine similarity = dot product ÷ (length of A × length of B). It measures the angle between two vectors, so it compares direction (meaning) and ignores vector length. A higher score means the sentences are closer in meaning. It's a ranking signal, not a percentage of correctness.
- **What surprised me:** the margins are thin. Query 1 hit top-1 by only 0.023 (0.495 vs 0.472), and an unrelated interest sentence came second.
- **The miss:** "Is software licensing or brand usage fee income covered, and what counts?" expected [11] and got [12] (0.556 vs 0.499). The query never says "royalties" and asks two things at once. All of its top 3 are royalties sentences, so retrieval found the right topic but not the right sentence.
- **Added query:** "What's the definition of royalties?" hits [11] at 0.730 (vs 0.687 for [12]). I first labelled it [12] by mistake and corrected the label to [11] after seeing the output. [12] contains the literal words "definition of royalties" and still lost, because embeddings match meaning rather than keywords.
- **Result:** 3 of 4 queries hit top-1. The software licensing query stays in as a known miss.
- **Learn-back:** doubling every number in one vector leaves the cosine score unchanged. The dot product doubles and that vector's length doubles, so the two cancel. Only the direction counts.
