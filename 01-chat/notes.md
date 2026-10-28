# 01-chat — notes

- **Built:** a console chat over `IChatClient` (OllamaSharp → local `qwen3.5:9b`). It streams the answer chunk by chunk, shows the model's thinking in grey, and keeps a `List<ChatMessage>` history so a later turn remembers earlier ones.
- **What I learned:** the model is stateless. "Memory" means resending the whole history every turn, so context, latency and cost grow with each message. That's why the thinking text stays out of history: it would make every later prompt bigger and slower for no benefit.
- **Don't get yet:** <!-- fill in: e.g. how long the history can get before the model's context window cuts it off, and what to do then -->
