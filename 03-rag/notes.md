# 03-rag — notes

The "How it works" line is mine (grammar fixed). Data swapped from the US–Bangladesh tax treaty to Apple's fiscal 2026
annual report (10-K) on 13 Nov; the swap and the re-run are Claude's, on my request. The treaty version and its results
are in git history.

- **How it works:** we embed both the inputs (the chunks) and the query string, then score each input against the query with cosine similarity (or another scoring function). A higher score means that input is more closely related to the query.
- **Built:** a console app that reads Apple's 10-K (123 pages) with PdfPig (`ContentOrderTextExtractor`), splits it at every
  `Item N.` heading (Item 1 Business, 1A Risk Factors, 7 MD&A, 8 Financial Statements …), cuts long items into pieces of
  ≤ 3,000 characters, embeds the pieces with `qwen3-embedding:0.6b` and ranks them against the query.
- **Two things the treaty didn't need:** (1) the table of contents lists every item ("Item 1. Business 3"), so a page with
  10+ item lines is skipped, or every item would start on the contents page; (2) an item can run for 40 pages (Item 8),
  so one chunk per item would be far too big for the prompt.
- **Result (retrieval):** "What were total net sales in fiscal 2026?" → **Item 7 top-1** (0.547), then Item 8 (0.524).
  "Where is the company headquartered?" → Item 2 top-1 (0.596).

## C04 — answer with citations

Re-run by Claude on 13 Nov with the same prompt code (mine, 10 Nov).

- **Result:** rules in the System message, question + 5 numbered sources in the User message, `qwen3.5:9b` with thinking off.
  - Total net sales → "$478,834 million [1]". **Right** (Item 8, p. 35 of the PDF).
  - Headquarters → "Cupertino, California [1]". **Right** (Item 2).
  - "What was Microsoft's revenue in fiscal 2026?" → "Not in the sources." **Right** refusal.
  - R&D → **wrong**. It says $31,370 million (that is the fiscal **2024** column of the table) and then "calculates"
    $46,954 million from lines that don't add up. The report says **$45,765 million** for fiscal 2026 (p. 35, and p. 30 in Item 7).
- **Why the R&D answer broke:** the income statement comes out of the PDF as rows of numbers with three year-columns
  (2026, 2025, 2024) and no column headers next to them, and the model picked the wrong column, then did arithmetic it
  wasn't asked to do. This is what the tool in 04-tools fixes: numbers come from a checked table, not from the model reading a PDF table.
- **Where the sources go:** the User message. The System message holds the fixed rules; the sources change every question and are untrusted text (a PDF could say "ignore the rules"), so they stay out of the System role.
- **Why `NumCtx` 16384:** at Ollama's default 4096 the prompt runs out of room, and Ollama doesn't error: it silently drops
  the start of the prompt, which is the rules. The refusal rule would be gone and the model would guess.
- **What I'd change:** `[n]` is the source number, and the program maps it back to the item (the `Sources:` line, commented
  out for now). With many reports, "Item 7" repeats, so only the source number points to one chunk.
