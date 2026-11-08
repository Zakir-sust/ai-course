# 03-rag — notes

The "How it works" line is mine (grammar fixed); the facts are from the run, written up by Claude at my request.

- **How it works:** we embed both the inputs (the chunks) and the query string, then score each input against the query with cosine similarity (or another scoring function). A higher score means that input is more closely related to the query.
- **Built:** a console app that reads the US–Bangladesh treaty (2004) with PdfPig (`ContentOrderTextExtractor`), splits it into one chunk per `ARTICLE N` heading, embeds the chunks with `qwen3-embedding:0.6b` and ranks them against the query.
- **Result:** 29 chunks (Articles 1–29); the largest is Article 11 (~4.9k characters). "withholding tax on dividends" → **Article 10 is top-1** (0.471), then 19, 14, 24, 16.
- **What I'd change:** the output prints each chunk's position in the list, not its article number. They match here only because the headings run 1–29 with no gaps; a protocol PDF (only a few amended articles) would mislabel them. Print `chunk.Article`, the first 60 characters, and only the top 5.
