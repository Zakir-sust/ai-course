using System.ComponentModel;                                    // [Description] on the tool and its parameters
using System.Text.Json;                                         // JsonSerializer for metrics.json
using Microsoft.Extensions.AI;                                  // IChatClient, ChatMessage, ChatOptions, AIFunctionFactory
using OllamaSharp;                                              // OllamaApiClient, AddOllamaOption
using OllamaSharp.Models;                                       // OllamaOption

// metrics.json: 5 rows from Apple's fiscal 2026 10-K (Items 1, 7, 8), read from the PDF. Copied next to the .dll on build.
var metrics = JsonSerializer.Deserialize<List<Metric>>(
    File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "metrics.json")),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
Console.WriteLine($"Loaded {metrics.Count} metrics: " + string.Join(", ", metrics.Select(m => $"{m.Name} {m.Value:N0}")));

// Same setup as 03-rag: 5-minute timeout, 16k context, thinking off.
IChatClient ollama = new OllamaApiClient(
    new HttpClient { BaseAddress = new Uri("http://localhost:11434"), Timeout = TimeSpan.FromMinutes(5) },
    "qwen3.5:9b");

var options = new ChatOptions();
options.AddOllamaOption(OllamaOption.NumCtx, 16384);
options.AddOllamaOption(OllamaOption.Think, false);

// TODO 1 (you): wrap `ollama` so tool calls actually run, and use that client below.
var chatClient = ollama.AsBuilder().UseFunctionInvocation().Build();

// TODO 2 (you): put GetMetric on options.Tools.
options.Tools = [AIFunctionFactory.Create(GetMetric)];

var questions = new[]
{
    "What was Apple's research and development expense in fiscal 2026?",       // Done-when question (03-rag got it wrong)
    "How much did Apple earn after tax in fiscal 2026?",                       // the model has to map the words to a metric
    "What was Microsoft's revenue in fiscal 2026?",                            // not in metrics.json
};

foreach (var question in questions)
{
    Console.WriteLine($"\n=== Q: {question}");

    // TODO 3 (you): the messages: your rules as System, the question as User.
    var messages = new List<ChatMessage>();
    var systemMessage = "use the tool for any number, cite the item, don’t guess";
    var system = new ChatMessage(ChatRole.System, systemMessage);
    messages.Add(system);
    messages.Add(new ChatMessage(ChatRole.User, question));
    // TODO 4 (you): plain answer first: GetResponseAsync, print the text. A "[tool] ..." line should print before it.
    var plainAns = await chatClient.GetResponseAsync(messages, options);
    Console.WriteLine("Ans: " + plainAns.Text);

    messages.AddMessages(plainAns);
    // Without a new user turn the history ends on the model's own answer, and call 2 sometimes never closes the JSON (3 of 5 replays).
    messages.Add(new ChatMessage(ChatRole.User, "Give that answer as JSON; citations are the item references."));

    var clonedOptions = options.Clone();
    clonedOptions.Tools = null;
    // TODO 5 (you): the typed answer: GetResponseAsync<MetricAnswer>, print Answer, Citations, Confidence.
    var secondAns = await chatClient.GetResponseAsync<MetricAnswer>(messages, clonedOptions);
    Console.WriteLine($"2nd ANs: {secondAns.Result.Answer}, {string.Join(",", secondAns.Result.Citations)}, {secondAns.Result.Confidence}");
}

// TODO 6 (you): the tool.
//   string GetMetric(string company, string metric) { ... }
//   [Description] on the function and on both parameters (say which values exist: "Apple"; revenue/net_income/rnd_expense/iphone_sales/employees).
//   Print "[tool] GetMetric(company, metric)", return the matching rows (JsonSerializer.Serialize) or a clear "no metric found".
[Description("Given a company and a metric, it returns the value from the company's annual report.")]
string GetMetric([Description("Apple")] string company, [Description("revenue/net_income/rnd_expense/iphone_sales/employees")] string metric)
{
    Console.WriteLine($"[tool] GetMetric({company}, {metric})");
    var matchedMetrics = metrics.FindAll(x => string.Equals(company, x.Company, StringComparison.OrdinalIgnoreCase) && string.Equals(metric, x.Name, StringComparison.OrdinalIgnoreCase));
    if (matchedMetrics.Count == 0)
    {
        return "No metric found";
    }

    return JsonSerializer.Serialize(matchedMetrics);
}

record Metric(string Company, string Name, double Value, string Unit, string Item, string Note);



// TODO 7 (you): record MetricAnswer(...) with Answer, Citations, Confidence.
record MetricAnswer(string Answer, string[] Citations, double Confidence);
