using System;
using Spectre.Console;

namespace duo_code.Utils
{
    public static class ConsoleHelper
    {
        // Standard console colors for consistency
        public static void WriteInfo(string message)
        {
            AnsiConsole.MarkupLine($"[dim]{EscapeMarkup(message)}[/]");
        }

        public static void WriteSuccess(string message)
        {
            AnsiConsole.MarkupLine($"[green]{EscapeMarkup(message)}[/]");
        }

        public static void WriteWarning(string message)
        {
            AnsiConsole.MarkupLine($"[yellow]{EscapeMarkup(message)}[/]");
        }

        public static void WriteError(string message)
        {
            AnsiConsole.MarkupLine($"[red]{EscapeMarkup(message)}[/]");
        }

        public static void WriteAssistantResponse(string response)
        {
            AnsiConsole.MarkupLine($"[yellow]>[/] {EscapeMarkup(response)}");
        }

        public static void WriteToolResult(string result)
        {
            AnsiConsole.MarkupLine($"[cyan]>[/] {result}");
        }

        public static void ShowProgress(string description, Action action)
        {
            AnsiConsole.Status()
                .Start(description, ctx =>
                {
                    action();
                });
        }

        public static async Task ShowProgressAsync(string description, Func<Task> asyncAction)
        {
            await AnsiConsole.Status()
                .StartAsync(description, async ctx =>
                {
                    await asyncAction();
                });
        }

        public static bool Confirm(string prompt)
        {
            return AnsiConsole.Confirm(prompt);
        }

        public static string Ask(string prompt)
        {
            return AnsiConsole.Ask<string>(prompt);
        }

        public static T Prompt<T>(IPrompt<T> prompt)
        {
            return AnsiConsole.Prompt(prompt);
        }

        // Simple console write methods that don't use markup
        public static void WriteLine(string? message = null)
        {
            Console.WriteLine(message);
        }

        public static void Write(string? message = null)
        {
            Console.Write(message);
        }

        // Color-coded console methods using standard Console colors
        public static void WriteLineColor(string message, ConsoleColor color)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ForegroundColor = oldColor;
        }

        // Helper method to escape markup for Spectre.Console
        private static string EscapeMarkup(string text)
        {
            return text.Replace("[", "[[").Replace("]", "]]");
        }
    }
}