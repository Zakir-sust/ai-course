// See https://aka.ms/new-console-template for more information
using System.Numerics.Tensors;
using System.Text; // TensorPrimitives.CosineSimilarity
using System.Text.RegularExpressions;                           // Regex.Matches for the "Article N" headings
using Microsoft.Extensions.AI;                                  // IEmbeddingGenerator, Embedding<float>
using OllamaSharp;                                              // OllamaApiClient
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


var chunks = GetChunks(pdfPath);
var query = "withholding tax on dividends";
await DoScore(query, chunks);

return;

async Task DoScore(string query, List<(int Article, string Text)> chunks)
{
    var chunkTexts = new List<string>();
    chunks.ForEach(x => chunkTexts.Add(x.Text));
    var embeddings = await generator.GenerateAsync(chunkTexts);
    var queryEmbedding = await generator.GenerateAsync(query);
    var scores = new List<(int, double)>();
    var idx = 1;
    foreach (var embedding in embeddings.ToList())
    {
        var score = TensorPrimitives.CosineSimilarity(embedding.Vector.Span, queryEmbedding.Vector.Span);
        scores.Add((idx, score));
        idx++;
    }

    scores = scores.OrderByDescending(x => x.Item2).ToList();
    scores.ForEach((x) => Console.WriteLine(x.Item1 + ":::" + x.Item2));
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