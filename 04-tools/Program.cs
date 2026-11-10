using System.ComponentModel;                                    // [Description] on the tool and its parameters
using System.Text.Json;                                         // JsonSerializer for rates.json
using Microsoft.Extensions.AI;                                  // IChatClient, ChatMessage, ChatOptions, AIFunctionFactory
using OllamaSharp;                                              // OllamaApiClient, AddOllamaOption
using OllamaSharp.Models;                                       // OllamaOption

// rates.json: 5 rows from the US–Bangladesh treaty (2004), Art. 10(2), 11(2), 11(3)(b), 12(2). Copied next to the .dll on build.
var rates = JsonSerializer.Deserialize<List<Rate>>(
    File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "rates.json")),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
Console.WriteLine($"Loaded {rates.Count} rates: " + string.Join(", ", rates.Select(r => $"{r.IncomeType} {r.Percent}%")));

// Same setup as 03-rag: 5-minute timeout, 16k context, thinking off.
IChatClient ollama = new OllamaApiClient(
    new HttpClient { BaseAddress = new Uri("http://localhost:11434"), Timeout = TimeSpan.FromMinutes(5) },
    "qwen3.5:9b");

var options = new ChatOptions();
options.AddOllamaOption(OllamaOption.NumCtx, 16384);
options.AddOllamaOption(OllamaOption.Think, false);

// TODO 1 (you): wrap `ollama` so tool calls actually run, and use that client below.
var chatClient = ollama.AsBuilder().UseFunctionInvocation().Build();

// TODO 2 (you): put GetRate on options.Tools.
options.Tools = [AIFunctionFactory.Create(GetRate)];

var questions = new[]
{
    "What is the dividend withholding rate between the US and Bangladesh?",     // Done-when question
    "What rate applies to interest paid from Bangladesh to a US bank?",         // the model has to pick by condition
    "What is the withholding rate on royalties between Bangladesh and India?",  // not in rates.json
};

foreach (var question in questions)
{
    Console.WriteLine($"\n=== Q: {question}");

    // TODO 3 (you): the messages: your rules as System, the question as User.
    var messages = new List<ChatMessage>();
    var systemMessage = "use the tool for any rate, cite the article, don’t guess";
    var system = new ChatMessage(ChatRole.System, systemMessage);
    messages.Add(system);
    messages.Add(new ChatMessage(ChatRole.User, question));
    // TODO 4 (you): plain answer first: GetResponseAsync, print the text. A "[tool] ..." line should print before it.
    var plainAns = await chatClient.GetResponseAsync(messages, options);
    Console.WriteLine("Ans: " + plainAns.Text);

    messages.AddMessages(plainAns);
    // Without a new user turn the history ends on the model's own answer, and call 2 sometimes never closes the JSON (3 of 5 replays).
    messages.Add(new ChatMessage(ChatRole.User, "Give that answer as JSON; citations are the article references."));

    var clonedOptions = options.Clone();
    clonedOptions.Tools = null;
    // TODO 5 (you): the typed answer: GetResponseAsync<RateAnswer>, print Answer, Citations, Confidence.
    var secondAns = await chatClient.GetResponseAsync<RateAnswer>(messages, clonedOptions);
    Console.WriteLine($"2nd ANs: {secondAns.Result.Answer}, {string.Join(",", secondAns.Result.Citations)}, {secondAns.Result.Confidence}");
}

// TODO 6 (you): the tool.
//   string GetRate(string treaty, string incomeType) { ... }
//   [Description] on the function and on both parameters (say which values exist: "US-Bangladesh"; dividends/interest/royalties).
//   Print "[tool] GetRate(treaty, incomeType)", return the matching rows (JsonSerializer.Serialize) or a clear "no rate found".
[Description("Given a treaty and incomeType, it returns the rate.")]
string GetRate([Description("US-Bangladesh")] string treaty, [Description("dividends/interest/royalties")] string incomeType)
{
    Console.WriteLine($"[tool] GetRate({treaty}, {incomeType})");
    var matchedRates = rates.FindAll(x => string.Equals(treaty, x.Treaty, StringComparison.OrdinalIgnoreCase) && string.Equals(incomeType, x.IncomeType, StringComparison.OrdinalIgnoreCase));
    if (matchedRates.Count == 0)
    {
        return "No rate found";
    }

    return JsonSerializer.Serialize(matchedRates);
}

record Rate(string Treaty, string IncomeType, double Percent, string Article, string Condition);



// TODO 7 (you): record RateAnswer(...) with Answer, Citations, Confidence.
record RateAnswer(string Answer, string[] Citations, double Confidence);
