# 03-rag — notes

The "How it works" line is mine (grammar fixed); the facts are from the run, written up by Claude at my request.

- **How it works:** we embed both the inputs (the chunks) and the query string, then score each input against the query with cosine similarity (or another scoring function). A higher score means that input is more closely related to the query.
- **Built:** a console app that reads the US–Bangladesh treaty (2004) with PdfPig (`ContentOrderTextExtractor`), splits it into one chunk per `ARTICLE N` heading, embeds the chunks with `qwen3-embedding:0.6b` and ranks them against the query.
- **Result:** 29 chunks (Articles 1–29); the largest is Article 11 (~4.9k characters). "withholding tax on dividends" → **Article 10 is top-1** (0.471), then 19, 14, 24, 16.
- **What I'd change:** the output prints each chunk's position in the list, not its article number. They match here only because the headings run 1–29 with no gaps; a protocol PDF (only a few amended articles) would mislabel them. Print `chunk.Article`, the first 60 characters, and only the top 5.

## C04 — answer with citations

Written by Claude from my chat answers (10 Nov), corrected where marked.

- **Result:** rules in the System message, question + 5 numbered sources in the User message, `qwen3.5:9b` with thinking off. Dividends → [1] = Art. 10 (15%, or 10% with a 10% stake), interest → [1] = Art. 11 plus [2] = Art. 24 (deductibility), PE → [1] = Art. 5, "corporate income tax rate in Bangladesh" → "Not in the sources." Nothing failed.
- **Where the sources go:** the User message. The System message holds the fixed rules; the sources change every question and are untrusted text (a PDF could say "ignore the rules"), so they stay out of the System role.
- **Why `NumCtx` 16384:** at Ollama's default 4096 the prompt runs out of room, and Ollama doesn't error: it silently drops the start of the prompt, which is the rules. The refusal rule would be gone and the model would guess a tax rate.
- **What I'd change:** drop "here n means article number" from the rules. `[n]` is the source number, and the program maps it back to the article (the `Sources:` line, commented out for now). With many treaties, "Article 10" repeats, so only the source number points to one chunk.
