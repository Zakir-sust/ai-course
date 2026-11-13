# 04-tools — notes

Written by Claude from my chat answers (10–11 Nov), corrected where marked. Boilerplate (project, the data file, client
setup) by Claude on request; the tool, the wiring and the two calls are mine. Data swapped on 13 Nov from 5 tax-treaty
rates (`rates.json`, `GetRate`) to 5 figures from Apple's fiscal 2026 annual report (`metrics.json`, `GetMetric`); the
swap and the re-run are Claude's. The treaty version is in git history.

- **How tool calling works (mine):** when methods are registered as tools, the model finds the parameters and calls the designated method.
  *Corrected:* the model only **asks** for the call: it returns the tool name and arguments (`GetMetric(Apple, rnd_expense)`). `UseFunctionInvocation()` (`FunctionInvokingChatClient`) runs `GetMetric`, sends the result back, and the model then writes the answer.
- **Why two calls (mine):** the previous prompt had JSON in it, which made the model fail sometimes; a good user prompt is better.
  *Corrected:* two separate problems, both found with the treaty data on 10 Nov (not re-run on the reports):
  1. One call with tools **and** the JSON schema (`GetResponseAsync<T>`): Ollama constrains the output to the schema, and the model never called the tool (0 of 3). It made the numbers up instead, at confidence 1. Fix: call 1 with tools → `AddMessages` → call 2 typed, without tools.
  2. Call 2 ending on the model's own answer ran away in 3 of 5 replays (never closed the JSON, 5-minute timeout). A user message "Give that answer as JSON…" fixed it, 5 of 5.
- **Run on the reports (13 Nov, one run):**
  - R&D → `[tool] GetMetric(Apple, rnd_expense)` → "$45.765 billion", typed answer 45765 / Item 8 / 0.95. ✓ This is the
    question 03-rag got **wrong** from the PDF text (it read the 2024 column): the tool fixed it.
  - "How much did Apple earn after tax?" → the model mapped the words to `net_income` by itself → $132,175 million, Item 8. ✓
  - Microsoft's revenue → `GetMetric(Microsoft, revenue)` → "No metric found" → refused. ✓ But its explanation was made up
    ("we are currently in 2024"): the refusal was right, the reason wasn't.
- **Watch:** "$45.765 billion (USD millions)" mixes two units in one sentence. The tool returns millions; the answer should
  say one unit. C18's validation can check that the number in the answer matches what the tool returned.
- **Confidence isn't a check:** the model says 0.95 or 1 whether it's right or wrong (it said 1 for a made-up treaty rate on
  10 Nov). It's just another generated field.
- **Carry forward:** C12 (refuse when it isn't in the sources), C17 (the answer must quote the tool's number), C18 (validate
  the typed answer: confidence 0–1, a number the tool actually returned, one unit).
