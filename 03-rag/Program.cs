using System.Numerics.Tensors;                                  // TensorPrimitives.CosineSimilarity
using System.Text;                                              // StringBuilder
using System.Text.RegularExpressions;                           // Regex.Matches for the "Item N." headings
using Microsoft.Extensions.AI;                                  // IEmbeddingGenerator, IChatClient, ChatMessage, ChatOptions
using OllamaSharp;                                              // OllamaApiClient, AddOllamaOption
using OllamaSharp.Models;                                       // OllamaOption
using UglyToad.PdfPig;                                          // PdfDocument
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;     // ContentOrderTextExtractor
var pdfPath = "/Users/purple_ghost/Documents/Myself/My Career/learning projects/findoc/sources/apple-fy2026.pdf";
if (!File.Exists(pdfPath))
{
    Console.WriteLine($"PDF not found: {pdfPath}");
    return;
}

IEmbeddingGenerator<string, Embedding<float>> generator =
    new OllamaApiClient(new Uri("http://localhost:11434"), "qwen3-embedding:0.6b");

// The default HttpClient gives up after 100 s; a long answer can take longer.
IChatClient chatClient = new OllamaApiClient(
    new HttpClient { BaseAddress = new Uri("http://localhost:11434"), Timeout = TimeSpan.FromMinutes(5) },
    "qwen3.5:9b");

// Ollama runs qwen3.5 with a 4096-token context by default; 5 item chunks + the rules don't fit,
// and Ollama drops the start of the prompt (the rules) without an error. Thinking off = answers in seconds.
var options = new ChatOptions();
options.AddOllamaOption(OllamaOption.NumCtx, 16384);
options.AddOllamaOption(OllamaOption.Think, false);

var chunks = GetChunks(pdfPath);
var embeddings = await generator.GenerateAsync(chunks.Select(x => x.Text));

var questions = new List<(string Question, string Expected)>
{
    ("What were total net sales in fiscal 2026?", "Item 7 or 8"),
    ("How much did the company spend on research and development?", "Item 7 or 8"),
    ("Where is the company headquartered?", "Item 2"),
    ("What was Microsoft's revenue in fiscal 2026?", "Not in the sources."),
};

foreach (var (question, expected) in questions)
{
    Console.WriteLine($"\n=== Q: {question}   (expect: {expected})");
    var res = await Ask(question);
    Console.WriteLine("Reply = " + res);
}

return;

async Task<string> Ask(string question)
{
    var top5 = await DoScore(question, chunks, embeddings);
    Console.WriteLine("Retrieved: " + string.Join(", ", top5.Select(x => $"Item {x.Item} ({x.Score:F3})")));

    var sb = new StringBuilder();
    foreach (var (i, value) in top5.Index())
    {
        sb.Append($"[{i + 1}] Item {value.Item}\n{value.Text}\n\n");
    }

    var systemPrompt = new List<string>
    {
        "Answer only from the sources.",
        "Cite every claim as [n]",
        "If the sources don't answer it, reply exactly: \"Not in the sources.\""
    };
    
    var messages = new List<ChatMessage>();
    var systemMessage = String.Join("\n", systemPrompt);
    messages.Add(new ChatMessage(ChatRole.System, systemMessage));
    messages.Add(new ChatMessage(ChatRole.User, sb.ToString() + "\n" + question));
    
    var reply = await chatClient.GetResponseAsync(messages, options);
    // Console.WriteLine("Answer: " + reply.Text);
    // Console.WriteLine("Sources: " + string.Join(", ", top5.Select((x, i) => $"[{i + 1}] = Item {x.Item}")));
    return reply.Text;
}

async Task<List<(string Item, string Text, double Score)>> DoScore(string query, List<(string Item, string Text)> chunks, GeneratedEmbeddings<Embedding<float>> embeddings)
{
    var queryEmbedding = await generator.GenerateAsync(query);
    var scores = new List<(string Item, string Text, double Score)>();
    foreach (var (i, embedding) in embeddings.ToList().Index())
    {
        var score = TensorPrimitives.CosineSimilarity(embedding.Vector.Span, queryEmbedding.Vector.Span);
        scores.Add((chunks[i].Item, chunks[i].Text, score));
    }

    return scores.OrderByDescending(x => x.Score).Take(5).ToList();
}

List<(string Item, string Text)> GetChunks(string filePath)
{
    using var pdfDoc = PdfDocument.Open(filePath);
    var pages = pdfDoc.GetPages().Select(p => ContentOrderTextExtractor.GetText(p)).ToList();

    // "Item 7. Management's Discussion …" alone on its line. The table of contents lists every item on one page
    // ("Item 1. Business 3"), so a page with 10+ of them is skipped; a real page has a handful at most.
    var heading = new Regex(@"^[ \t]*Item[ \t]+(\d{1,2}[A-C]?)\.", RegexOptions.Multiline | RegexOptions.IgnoreCase);
    var fullText = string.Join("\n", pages.Select(p => heading.Matches(p).Count >= 10 ? "" : p));
    var headings = heading.Matches(fullText);

    // A 10-K item can run for 40 pages (Item 8), far past what fits in a prompt, so each item is cut into
    // pieces of at most ~3,000 characters at line breaks. Every piece keeps its item number for the citation.
    const int maxChars = 3000;
    var chunks = new List<(string, string)>();
    for (int i = 0; i < headings.Count; i++)
    {
        int start = headings[i].Index;
        int end = i + 1 < headings.Count ? headings[i + 1].Index : fullText.Length;
        var item = headings[i].Groups[1].Value.ToUpperInvariant();

        var piece = new StringBuilder();
        foreach (var line in fullText[start..end].Split('\n'))
        {
            if (piece.Length + line.Length > maxChars && piece.Length > 0)
            {
                chunks.Add((item, piece.ToString().Trim()));
                piece.Clear();
            }
            piece.Append(line).Append('\n');
        }
        if (piece.ToString().Trim().Length > 0) chunks.Add((item, piece.ToString().Trim()));
    }
    return chunks;
}
