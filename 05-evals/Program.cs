using System.Numerics.Tensors;                                  // TensorPrimitives.CosineSimilarity
using System.Runtime.CompilerServices;                          // CallerFilePath
using System.Text;                                              // StringBuilder
using System.Text.Json;                                         // answers.json
using System.Text.RegularExpressions;                           // the "Item N." headings
using Microsoft.Extensions.AI;                                  // IChatClient, ChatMessage, ChatResponse, ChatOptions
using Microsoft.Extensions.AI.Evaluation;                       // ChatConfiguration, IEvaluator, CompositeEvaluator, NumericMetric
using Microsoft.Extensions.AI.Evaluation.Quality;               // RelevanceEvaluator, GroundednessEvaluator, GroundednessEvaluatorContext
using OllamaSharp;                                              // OllamaApiClient, AddOllamaOption
using OllamaSharp.Models;                                       // OllamaOption
using UglyToad.PdfPig;                                          // PdfDocument
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;     // ContentOrderTextExtractor

// C06: an LLM judge (MEAI.Evaluation) scores the C04 RAG answers. I score them first, by hand.
// Run 1: no answers.json yet → answer 5 questions with the C04 pipeline, save them, stop.
//        Score all 5 in notes.md BEFORE run 2.
// Run 2: answers.json exists → the judge scores exactly those answers (a fresh run would give different text).
// Delete answers.json to start over.

var answersPath = Path.Combine(ProjectDir(), "answers.json");

// The default HttpClient gives up after 100 s; a judge call with 5 long sources can take longer.
IChatClient chatClient = new OllamaApiClient(
    new HttpClient { BaseAddress = new Uri("http://localhost:11434"), Timeout = TimeSpan.FromMinutes(5) },
    "qwen3.5:9b");

if (!File.Exists(answersPath))
{
    var rows = await GenerateAnswers();
    File.WriteAllText(answersPath, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
    foreach (var (i, row) in rows.Index())
    {
        Console.WriteLine($"\n#{i + 1} Q: {row.Question}   (expect: {row.Expected})");
        Console.WriteLine("Sources: " + string.Join(", ", row.Items.Select((a, n) => $"[{n + 1}] = Item {a}")));
        Console.WriteLine("A: " + row.Answer);
    }
    Console.WriteLine($"\nSaved {rows.Count} answers to answers.json.");
    Console.WriteLine("Now score all 5 in notes.md (relevance + groundedness, 1-5). Then run again for the judge.");
    return;
}

var answers = JsonSerializer.Deserialize<List<AnswerRow>>(File.ReadAllText(answersPath))!;
Console.WriteLine($"Judging {answers.Count} saved answers from answers.json ...");
var table = new List<string> { "| # | Question | Judge R | Judge G |", "|---|---|---|---|" };
var unparsed = new List<string>(); // one line per score the judge's reply didn't give us: never dropped silently

// ---- The judge: your part ----------------------------------------------------------------------

// TODO 1: the judge's client. The evaluators build their own ChatOptions, so NumCtx 16384 + thinking off
//         (the `options` C04 passed to GetResponseAsync) never reach Ollama. Wrap chatClient so every call gets them:
//         chatClient.AsBuilder().ConfigureOptions(o => { ... }).Build()
IChatClient judgeClient = chatClient.AsBuilder()
    .ConfigureOptions(o =>
    {
        o.AddOllamaOption(OllamaOption.NumCtx, 16384);
        o.AddOllamaOption(OllamaOption.Think, false);
    }).Build();

// TODO 2: a ChatConfiguration around judgeClient (it tells the evaluators which model is the judge).
ChatConfiguration? judgeConfig = new ChatConfiguration(judgeClient);

// TODO 3: one evaluator that runs both: a CompositeEvaluator of a RelevanceEvaluator and a GroundednessEvaluator.
IEvaluator? evaluator = new CompositeEvaluator(new RelevanceEvaluator(), new GroundednessEvaluator());

foreach (var (i, row) in answers.Index())
{
    // TODO 4: the conversation the judge sees: the question alone, as one User message.
    //         (Not the sources. Relevance = does the answer address the question?)

    var chatMessage = new ChatMessage(ChatRole.User, row.Question);
    List<ChatMessage> messages  = [chatMessage];
    // TODO 5: the response being judged: a ChatResponse holding one Assistant message with row.Answer.
    var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, row.Answer));

    // TODO 6: evaluator.EvaluateAsync(messages, response, judgeConfig, additionalContext)
    //         additionalContext = [ a GroundednessEvaluatorContext with row.Sources ]
    //         (Groundedness = is every claim in the answer backed by these sources?)
    var result = await evaluator.EvaluateAsync(messages, response, judgeConfig, [new GroundednessEvaluatorContext(row.Sources)]);
    // TODO 7: pull both scores out of the result with Get<NumericMetric>(...) and the name constants
    //         RelevanceEvaluator.RelevanceMetricName / GroundednessEvaluator.GroundednessMetricName, then uncomment:
    var relevance = result.Get<NumericMetric>(RelevanceEvaluator.RelevanceMetricName);
    var groundedness = result.Get<NumericMetric>(GroundednessEvaluator.GroundednessMetricName);
    PrintScores(i + 1, row, relevance, groundedness);
}

Console.WriteLine("\n" + string.Join("\n", table));

// The evidence for every "-" in the table: which score, and the judge's own diagnostic.
Console.WriteLine($"\nUnparsed: {unparsed.Count} of {answers.Count * 2} scores");
foreach (var line in unparsed)
    Console.WriteLine("  " + line);
return;

// ---- Helpers (boilerplate) ---------------------------------------------------------------------

// Prints one judged row and adds it to the summary table.
// A null Value means the judge's reply couldn't be parsed; the diagnostics say why.
void PrintScores(int n, AnswerRow row, NumericMetric relevance, NumericMetric groundedness)
{
    Console.WriteLine($"\n#{n} {row.Question}");
    foreach (var metric in new[] { relevance, groundedness })
    {
        Console.WriteLine($"  {metric.Name}: {metric.Value?.ToString() ?? "-"} ({metric.Interpretation?.Rating})");
        Console.WriteLine($"    why: {metric.Reason}");
        foreach (var d in metric.Diagnostics ?? [])
            Console.WriteLine($"    {d.Severity}: {d.Message}");

        if (metric.Value is null)
        {
            // Prefer the Error diagnostic (the parse failure) over the Informational chain of thought.
            var cause = (metric.Diagnostics ?? []).OrderByDescending(d => d.Severity).FirstOrDefault();
            var message = cause is null ? "no diagnostic" : $"{cause.Severity}: {OneLine(cause.Message, 200)}";
            unparsed.Add($"#{n} {metric.Name}: {message}");
        }
    }
    table.Add($"| {n} | {row.Question} | {relevance.Value?.ToString() ?? "-"} | {groundedness.Value?.ToString() ?? "-"} |");
}

// The C04 pipeline (03-rag), unchanged: chunk by 10-K item, embed, top 5, rules in System, sources + question in User.
async Task<List<AnswerRow>> GenerateAnswers()
{
    var pdfPath = "/Users/purple_ghost/Documents/Myself/My Career/learning projects/findoc/sources/apple-fy2026.pdf";
    if (!File.Exists(pdfPath))
        throw new FileNotFoundException($"PDF not found: {pdfPath}");

    IEmbeddingGenerator<string, Embedding<float>> generator =
        new OllamaApiClient(new Uri("http://localhost:11434"), "qwen3-embedding:0.6b");

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
        ("What was net income in fiscal 2026?", "Item 8"),
    };

    var systemMessage = string.Join("\n",
        "Answer only from the sources.",
        "Cite every claim as [n]",
        "If the sources don't answer it, reply exactly: \"Not in the sources.\"");

    var rows = new List<AnswerRow>();
    foreach (var (question, expected) in questions)
    {
        Console.WriteLine($"Answering: {question}");
        var top5 = await TopFive(generator, question, chunks, embeddings);

        var sb = new StringBuilder();
        foreach (var (i, chunk) in top5.Index())
            sb.Append($"[{i + 1}] Item {chunk.Item}\n{chunk.Text}\n\n");
        var sources = sb.ToString();

        List<ChatMessage> messages =
        [
            new(ChatRole.System, systemMessage),
            new(ChatRole.User, sources + "\n" + question),
        ];
        var reply = await chatClient.GetResponseAsync(messages, options);

        rows.Add(new AnswerRow(question, expected, reply.Text, sources, top5.Select(x => x.Item).ToList()));
    }
    return rows;
}

async Task<List<(string Item, string Text, double Score)>> TopFive(
    IEmbeddingGenerator<string, Embedding<float>> generator, string query,
    List<(string Item, string Text)> chunks, GeneratedEmbeddings<Embedding<float>> embeddings)
{
    var queryEmbedding = await generator.GenerateAsync(query);
    return embeddings
        .Select((e, i) => (chunks[i].Item, chunks[i].Text,
            Score: (double)TensorPrimitives.CosineSimilarity(e.Vector.Span, queryEmbedding.Vector.Span)))
        .OrderByDescending(x => x.Score)
        .Take(5)
        .ToList();
}

// The same chunking as 03-rag: split at "Item N." headings (table-of-contents page skipped), long items cut at ~3,000 characters.
List<(string Item, string Text)> GetChunks(string filePath)
{
    using var pdfDoc = PdfDocument.Open(filePath);
    var pages = pdfDoc.GetPages().Select(p => ContentOrderTextExtractor.GetText(p)).ToList();
    var heading = new Regex(@"^[ \t]*Item[ \t]+(\d{1,2}[A-C]?)\.", RegexOptions.Multiline | RegexOptions.IgnoreCase);
    var fullText = string.Join("\n", pages.Select(p => heading.Matches(p).Count >= 10 ? "" : p));
    var headings = heading.Matches(fullText);

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

// The folder of this file, so answers.json sits next to Program.cs whether you run from the terminal or Rider (bin/).
static string ProjectDir([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

// A diagnostic can carry the judge's whole reply: squash it to one line and cut it, so the summary stays readable.
static string OneLine(string text, int max)
{
    var line = Regex.Replace(text, @"\s+", " ").Trim();
    return line.Length <= max ? line : line[..max] + " …";
}

record AnswerRow(string Question, string Expected, string Answer, string Sources, List<string> Items);
