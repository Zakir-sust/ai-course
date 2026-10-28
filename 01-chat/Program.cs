// See https://aka.ms/new-console-template for more information

using System.Text;
using Microsoft.Extensions.AI;
using OllamaSharp;



IChatClient client = new OllamaApiClient(new Uri("http://localhost:11434"), "qwen3.5:9b");

var history = new List<ChatMessage>();
while (true)
{
    Console.WriteLine("Enter your input");
    var input = Console.ReadLine();
    if (input is null || input == "#") break;
    history.Add(new ChatMessage(ChatRole.User, input));
    var res = await GetResponse(history);
    history.Add(new ChatMessage(ChatRole.Assistant, res));
}


async Task<string> GetResponse(List<ChatMessage> h){
    var res = client.GetStreamingResponseAsync(h);
    var s = new StringBuilder();
    await foreach (var u in res)
    {
        foreach (var c in u.Contents)
        {
            if (c is TextReasoningContent r)
            {
                // Thinking is shown but kept out of history: it would grow every later prompt.
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(r.Text);
                Console.ResetColor();
            }
            else if (c is TextContent t)
            {
                Console.Write(t.Text);
                s.Append(t.Text);
            }
        }
    }
    Console.WriteLine();

    return s.ToString();
}
