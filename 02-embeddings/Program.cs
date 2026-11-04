using System.Numerics.Tensors;
using Microsoft.Extensions.AI;
using OllamaSharp;

IEmbeddingGenerator<string, Embedding<float>> generator =
    new OllamaApiClient(new Uri("http://localhost:11434"), "qwen3-embedding:0.6b");

// TODO 1: embed all of Data.Sentences as one batch.
var embedding = await generator.GenerateAsync(Data.Sentences);
// TODO 2: print the dimension (length of one vector).
Console.WriteLine($"total vectors: {embedding.Count}, dimensions {embedding[0].Dimensions}");

foreach (var (text, expected) in Data.Queries)
{
    // TODO 3: embed the query.
    var queryEmbedding = await generator.GenerateAsync(text);
    var scored = new List<(int Index, float Score)>();

    // TODO 4: cosine similarity of the query against every sentence; add (index, score) to scored.
    foreach (int i in Enumerable.Range(0, embedding.Count))
    {
        var singleEmbedding = embedding[i];
        var score = TensorPrimitives.CosineSimilarity(singleEmbedding.Vector.Span, queryEmbedding.Vector.Span);
        scored.Add((i, score));
    }
    PrintTop3(text, expected, scored);
}

static void PrintTop3(string query, int expected, List<(int Index, float Score)> scored)
{
    var top = scored.OrderByDescending(s => s.Score).Take(3).ToList();
    var hit = top.Count > 0 && top[0].Index == expected;

    Console.WriteLine($"\n{(hit ? "HIT " : "MISS")} | {query}");
    foreach (var (index, score) in top)
        Console.WriteLine($"  {score:F3}  [{index}] {Data.Sentences[index]}");
}
