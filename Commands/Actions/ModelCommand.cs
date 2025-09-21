using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using duo_code.Commands.Core;
using duo_code.Services;
using Spectre.Console;

namespace duo_code.Commands.Actions
{
    public class ModelCommand : ICommand, IHasAliases
    {
        public string Name => "model";
        public string Description => "Change the AI model and provider";
        public CommandType Type => CommandType.Action;
        public string[] GetAliases() => new[] { "m" };

        public Task<CommandResult> ExecuteAsync(string[] args)
        {
            return ShowInteractiveModelSelection();
        }

        private Task<CommandResult> ShowInteractiveModelSelection()
        {
            try
            {
                // Show current selection
                var currentProvider = CurrentState.Provider;
                var currentModel = CurrentState.Model;
                
                var currentPanel = new Panel($"[bold]Provider:[/] {currentProvider}\n[bold]Model:[/] {currentModel}")
                {
                    Header = new PanelHeader("[green]Current Selection[/]"),
                    Border = BoxBorder.Rounded,
                    BorderStyle = Style.Parse("green")
                };
                AnsiConsole.Write(currentPanel);
                AnsiConsole.WriteLine();

                // Create selection options
                var allModels = GetAllModelsWithDetails();
                var choices = allModels.Select(m => 
                {
                    var isCurrent = m.Provider == currentProvider && m.Model == currentModel;
                    var marker = isCurrent ? " [green](current)[/]" : "";
                    return $"[bold {GetProviderColor(m.Provider)}]{m.Provider}[/] - {m.Model}{marker}";
                }).ToList();

                var selection = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[bold cyan]Select AI Model:[/]")
                        .PageSize(10)
                        .MoreChoicesText("[grey](Move up and down to reveal more models)[/]")
                        .AddChoices(choices));

                // Find the selected model
                var selectedIndex = choices.IndexOf(selection);
                var selectedModel = allModels[selectedIndex];

                // Update settings
                CurrentState.Provider = selectedModel.Provider;
                CurrentState.Model = selectedModel.Model;
                
                // Save the settings
                ApiKeyManager.SaveCurrentSettings(CurrentState.Provider, CurrentState.Model);

                // Show success message
                var successPanel = new Panel($"[bold]Provider:[/] {selectedModel.Provider}\n[bold]Model:[/] {selectedModel.Model}")
                {
                    Header = new PanelHeader("[green]✓ Selection Updated[/]"),
                    Border = BoxBorder.Rounded,
                    BorderStyle = Style.Parse("green")
                };
                AnsiConsole.Write(successPanel);

                return Task.FromResult(CommandResult.Ok(""));
            }
            catch (Exception ex)
            {
                return Task.FromResult(CommandResult.Error($"Error during model selection: {ex.Message}"));
            }
        }

        private List<(ApiProvider Provider, string Model)> GetAllModelsWithDetails()
        {
            var result = new List<(ApiProvider, string)>();

            foreach (var provider in CurrentState.AvailableModels)
            {
                foreach (var model in provider.Value)
                {
                    result.Add((provider.Key, model));
                }
            }

            return result;
        }

        private string GetProviderColor(ApiProvider provider)
        {
            return provider switch
            {
                ApiProvider.Cerebras => "cyan",
                ApiProvider.Gemini => "green",
                _ => "white"
            };
        }

        private Task<CommandResult> ShowProviders()
        {
            var currentProvider = CurrentState.Provider;
            var providers = string.Join("\n", Enum.GetNames<ApiProvider>().Select(p => 
                p == currentProvider.ToString() ? $"  {p} (current)" : $"  {p}"));
            var message = $"Available providers:\n{providers}\n\nUsage: /model provider <name>";
            return Task.FromResult(CommandResult.Ok(message));
        }

        private Task<CommandResult> ShowAvailableModels()
        {
            var allModels = GetAllModelsWithNumbers();
            var currentProvider = CurrentState.Provider;
            var currentModel = CurrentState.Model;
            
            var modelList = string.Join("\n", allModels.Select(kvp => 
            {
                var isCurrentModel = kvp.Value.Provider == currentProvider && kvp.Value.Model == currentModel;
                var marker = isCurrentModel ? " (current)" : "";
                return $"  {kvp.Key}. [{kvp.Value.Provider}] {kvp.Value.Model}{marker}";
            }));
            
            var message = $"Current Provider: {currentProvider}\nAvailable models:\n{modelList}\n\nUsage: /model <number> or /model provider <name>";
            return Task.FromResult(CommandResult.Ok(message));
        }

        private Dictionary<string, (ApiProvider Provider, string Model)> GetAllModelsWithNumbers()
        {
            var result = new Dictionary<string, (ApiProvider, string)>();
            int counter = 1;

            foreach (var provider in CurrentState.AvailableModels)
            {
                foreach (var model in provider.Value)
                {
                    result[counter.ToString()] = (provider.Key, model);
                    counter++;
                }
            }

            return result;
        }
    }
}