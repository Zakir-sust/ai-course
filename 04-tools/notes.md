# 04-tools — notes

Written by Claude from my chat answers (10–11 Nov), corrected where marked. Boilerplate (project, `rates.json` from the treaty PDF, client setup) by Claude on request; the tool, the wiring and the two calls are mine.

- **How tool calling works (mine):** when methods are registered as tools, the model finds the parameters and calls the designated method.
  *Corrected:* the model only **asks** for the call: it returns the tool name and arguments (`GetRate(US-Bangladesh, dividends)`). `UseFunctionInvocation()` (`FunctionInvokingChatClient`) runs `GetRate`, sends the result back, and the model then writes the answer.
- **Why two calls (mine):** the previous prompt had JSON in it, which made the model fail sometimes; a good user prompt is better.
  *Corrected:* two separate problems.
  1. One call with tools **and** the JSON schema (`GetResponseAsync<T>`): Ollama constrains the output to the schema, and the model never called the tool (0 of 3). It made rates up instead: dividends 0% at confidence 1, bank interest 10%. Fix: call 1 with tools → `AddMessages` → call 2 typed, without tools.
  2. Call 2 ending on the model's own answer ran away in 3 of 5 replays (never closed the JSON, 5-minute timeout). A user message "Give that answer as JSON…" fixed it, 5 of 5. So the user prompt was the fix for this one.
- **Confidence isn't a check:** the model said 1 for a made-up 0%, and "100" in the last run (the scale was never given). It's just another generated field.
- **Final run (11 Nov, ~00:15):**
  - Dividends → `[tool] GetRate(US-Bangladesh, dividends)` → 10% / 15%, Art. 10(2)(a)/(b), confidence 0.98. ✓
  - Interest to a US bank → the tool returned 5% (Art. 11(3)(b)), but the model overrode it with "0%" from "standard treaty interpretation". ✗ (The run before said 5% correctly.)
  - Bangladesh–India royalties → the model called `GetRate(US-Bangladesh, royalties)` and answered for the wrong treaty, confidence 100. ✗ (The run before refused correctly.) The tool's description lists only "US-Bangladesh", which nudges it to force that value.
- **Carry forward:** C12 (refuse when it isn't in the sources), C17 (the answer must quote the tool's rate), C18 (validate the typed answer: confidence 0–1, a rate the tool actually returned).
