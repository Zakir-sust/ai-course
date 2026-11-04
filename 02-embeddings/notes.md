# 02-embeddings — notes

Written by Claude at my request, from my code and its output.

- **Built:** a console app that embeds 20 treaty-style sentences with `qwen3-embedding:0.6b`, embeds each query, scores it against every sentence with cosine similarity, and prints the top 3.
- **Vector dimension:** 1024.
- **Why cosine:** cosine similarity = dot product ÷ (length of A × length of B). It measures the angle between two vectors, so it compares direction (meaning) and ignores vector length. A higher score means the sentences are closer in meaning. It's a ranking signal, not a percentage of correctness.
- **What surprised me:** the margins are thin. Query 1 hit top-1 by only 0.023 (0.495 vs 0.472), and an unrelated interest sentence came second.
- **The miss:** "Is software licensing or brand usage fee income covered, and what counts?" expected [11] and got [12] (0.556 vs 0.499). The query never says "royalties" and asks two things at once. All of its top 3 are royalties sentences, so retrieval found the right topic but not the right sentence.
- **Added query:** "What's the definition of royalties?" is labelled [12] and prints MISS, but it retrieved [11] at 0.730 (vs 0.687 for [12]). [11] is the sentence that actually defines royalties, so the label looks wrong rather than the retrieval. Also, [12] contains the literal words "definition of royalties" and still lost.
- **Result so far:** the program prints 2 HIT and 2 MISS out of 4. It would be 3 of 4 if the added query's label were [11].
- **Still to learn:** I can say cosine uses the dot product. I still need to explain the division by the two lengths, and why that makes length not matter.
