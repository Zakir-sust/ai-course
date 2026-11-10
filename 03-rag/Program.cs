using System.Numerics.Tensors;                                  // TensorPrimitives.CosineSimilarity
using System.Text;                                              // StringBuilder
using System.Text.RegularExpressions;                           // Regex.Matches for the "Article N" headings
using Microsoft.Extensions.AI;                                  // IEmbeddingGenerator, IChatClient, ChatMessage, ChatOptions
using OllamaSharp;                                              // OllamaApiClient, AddOllamaOption
using OllamaSharp.Models;                                       // OllamaOption
using UglyToad.PdfPig;                                          // PdfDocument
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;     // ContentOrderTextExtractor
var pdfPath = "/Users/purple_ghost/Documents/Myself/My Career/learning projects/taxdoc/sources/us-bangladesh-2004.pdf";
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

// Ollama runs qwen3.5 with a 4096-token context by default; 5 article chunks + the rules don't fit,
// and Ollama drops the start of the prompt (the rules) without an error. Thinking off = answers in seconds.
var options = new ChatOptions();
options.AddOllamaOption(OllamaOption.NumCtx, 16384);
options.AddOllamaOption(OllamaOption.Think, false);

var chunks = GetChunks(pdfPath);
var embeddings = await generator.GenerateAsync(chunks.Select(x => x.Text));

var questions = new List<(string Question, string Expected)>
{
    ("What is the maximum withholding rate on dividends?", "Art. 10"),
    ("How is interest taxed?", "Art. 11"),
    ("What counts as a permanent establishment?", "Art. 5"),
    ("What is the corporate income tax rate in Bangladesh?", "Not in the sources."),
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
    Console.WriteLine("Retrieved: " + string.Join(", ", top5.Select(x => $"Art. {x.Article} ({x.Score:F3})")));

    var sb = new StringBuilder();
    foreach (var (i, value) in top5.Index())
    {
        sb.Append($"[{i + 1}] Article {value.Article}\n{value.Text}\n\n");
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
    // Console.WriteLine("Sources: " + string.Join(", ", top5.Select((x, i) => $"[{i + 1}] = Art. {x.Article}")));
    return reply.Text;
}

async Task<List<(int Article, string Text, double Score)>> DoScore(string query, List<(int Article, string Text)> chunks, GeneratedEmbeddings<Embedding<float>> embeddings)
{
    var queryEmbedding = await generator.GenerateAsync(query);
    var scores = new List<(int Article, string Text, double Score)>();
    foreach (var (i, embedding) in embeddings.ToList().Index())
    {
        var score = TensorPrimitives.CosineSimilarity(embedding.Vector.Span, queryEmbedding.Vector.Span);
        scores.Add((chunks[i].Article, chunks[i].Text, score));
    }

    return scores.OrderByDescending(x => x.Score).Take(5).ToList();
}

List<(int Article, string Text)> GetChunks(string filePath)
{
    using var pdfDoc = PdfDocument.Open(filePath);
    var fullText = string.Join("\n", pdfDoc.GetPages().Select(p => ContentOrderTextExtractor.GetText(p)));

    var headings = Regex.Matches(fullText, @"^ARTICLE (\d+)\s*$", RegexOptions.Multiline);

    var chunks = new List<(int, string)>();
    for (int i = 0; i < headings.Count; i++)
    {
        int start = headings[i].Index;
        int end = i + 1 < headings.Count ? headings[i + 1].Index : fullText.Length;
        chunks.Add((int.Parse(headings[i].Groups[1].Value), fullText[start..end].Trim()));
    }
    return chunks;
}
