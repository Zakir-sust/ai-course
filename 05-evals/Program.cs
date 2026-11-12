using System.Numerics.Tensors;                                  // TensorPrimitives.CosineSimilarity
using System.Runtime.CompilerServices;                          // CallerFilePath
using System.Text;                                              // StringBuilder
using System.Text.Json;                                         // answers.json
using System.Text.RegularExpressions;                           // the "ARTICLE N" headings
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
        Console.WriteLine("Sources: " + string.Join(", ", row.Articles.Select((a, n) => $"[{n + 1}] = Art. {a}")));
        Console.WriteLine("A: " + row.Answer);
    }
    Console.WriteLine($"\nSaved {rows.Count} answers to answers.json.");
    Console.WriteLine("Now score all 5 in notes.md (relevance + groundedness, 1-5). Then run again for the judge.");
    return;
}

var answers = JsonSerializer.Deserialize<List<AnswerRow>>(File.ReadAllText(answersPath))!;
Console.WriteLine($"Judging {answers.Count} saved answers from answers.json ...");
var table = new List<string> { "| # | Question | Judge R | Judge G |", "|---|---|---|---|" };

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
    }
    table.Add($"| {n} | {row.Question} | {relevance.Value?.ToString() ?? "-"} | {groundedness.Value?.ToString() ?? "-"} |");
}

// The C04 pipeline, unchanged: chunk by article, embed, top 5, rules in System, sources + question in User.
async Task<List<AnswerRow>> GenerateAnswers()
{
    var pdfPath = "/Users/purple_ghost/Documents/Myself/My Career/learning projects/taxdoc/sources/us-bangladesh-2004.pdf";
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
        ("What is the maximum withholding rate on dividends?", "Art. 10"),
        ("How is interest taxed?", "Art. 11"),
        ("What counts as a permanent establishment?", "Art. 5"),
        ("What is the corporate income tax rate in Bangladesh?", "Not in the sources."),
        ("What is the maximum withholding rate on royalties?", "Art. 12"),
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
            sb.Append($"[{i + 1}] Article {chunk.Article}\n{chunk.Text}\n\n");
        var sources = sb.ToString();

        List<ChatMessage> messages =
        [
            new(ChatRole.System, systemMessage),
            new(ChatRole.User, sources + "\n" + question),
        ];
        var reply = await chatClient.GetResponseAsync(messages, options);

        rows.Add(new AnswerRow(question, expected, reply.Text, sources, top5.Select(x => x.Article).ToList()));
    }
    return rows;
}

async Task<List<(int Article, string Text, double Score)>> TopFive(
    IEmbeddingGenerator<string, Embedding<float>> generator, string query,
    List<(int Article, string Text)> chunks, GeneratedEmbeddings<Embedding<float>> embeddings)
{
    var queryEmbedding = await generator.GenerateAsync(query);
    return embeddings
        .Select((e, i) => (chunks[i].Article, chunks[i].Text,
            Score: (double)TensorPrimitives.CosineSimilarity(e.Vector.Span, queryEmbedding.Vector.Span)))
        .OrderByDescending(x => x.Score)
        .Take(5)
        .ToList();
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

// The folder of this file, so answers.json sits next to Program.cs whether you run from the terminal or Rider (bin/).
static string ProjectDir([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

record AnswerRow(string Question, string Expected, string Answer, string Sources, List<int> Articles);
