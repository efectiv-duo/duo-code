using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using duo_code.Commands.Actions;

namespace duo_code.Commands.Core
{
    public class CommandRegistry
    {
        private readonly Dictionary<string, ICommand> _commands = new(StringComparer.OrdinalIgnoreCase);
        private readonly string? _promptsDirectory;

        public CommandRegistry(string? promptsDirectory = null)
        {
            _promptsDirectory = promptsDirectory ?? GetDefaultPromptsDirectory();
            RegisterCommands();
            LoadPromptCommands();
        }

        private string GetDefaultPromptsDirectory()
        {
            var possiblePaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Commands", "Prompts"),
                Path.Combine(Directory.GetCurrentDirectory(), "Commands", "Prompts"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Commands", "Prompts")
            };

            return possiblePaths.FirstOrDefault(Directory.Exists) ?? possiblePaths[0];
        }

        private void RegisterCommands()
        {
            // Register action commands explicitly
            Register(new ModelCommand());
            Register(new HelpCommand(this));
            Register(new ClearCommand());
            Register(new ExitCommand());
            Register(new ChallengeCommand());
            Register(new ApiKeyCommand());
            Register(new StatsCommand());
        }

        private void LoadPromptCommands()
        {
            if (string.IsNullOrEmpty(_promptsDirectory) || !Directory.Exists(_promptsDirectory))
            {
                return;
            }

            var mdFiles = Directory.GetFiles(_promptsDirectory, "*.md");

            foreach (var file in mdFiles)
            {
                try
                {
                    var command = new PromptCommand(file);
                    Register(command);
                }
                catch (Exception ex)
                {
                    // Log error but don't use Console.WriteLine in headless mode
                    // This will be handled by the error reporting system
                    System.Diagnostics.Debug.WriteLine($"Failed to load prompt command from {file}: {ex.Message}");
                }
            }
        }

        private void Register(ICommand command)
        {
            _commands[command.Name] = command;

            // Register aliases if command has them

            // if (command is ModelCommand modelCmd)
            // {
            //     _commands["m"] = modelCmd;
            // }
            // else if (command is HelpCommand helpCmd)
            // {
            //     _commands["h"] = helpCmd;
            //     _commands["?"] = helpCmd;
            // }
            // else if (command is ExitCommand exitCmd)
            // {
            //     _commands["quit"] = exitCmd;
            //     _commands["q"] = exitCmd;
            // }
            // else if (command is ClearCommand clearCmd)
            // {
            //     _commands["cls"] = clearCmd;
            // }
            if (command is IHasAliases aliasCommand)
            {
                foreach (var alias in aliasCommand.GetAliases())
                {
                    _commands[alias] = command;
                }
            }
        }

        public ICommand? GetCommand(string name)
        {
            return _commands.TryGetValue(name, out var command) ? command : null;
        }

        public IEnumerable<ICommand> GetAllCommands()
        {
            return _commands.Values
                .Distinct() // Remove duplicates from aliases
                .OrderBy(c => c.Name);
        }

        public IEnumerable<(string name, string description)> GetCommandList()
        {
            var seen = new HashSet<ICommand>();
            foreach (var kvp in _commands.OrderBy(x => x.Key))
            {
                if (seen.Add(kvp.Value))
                {
                    yield return (kvp.Key, kvp.Value.Description);
                }
            }
        }

        public IEnumerable<string> GetAllCommandNames()
        {
            return _commands.Keys.OrderBy(x => x);
        }
    }
}