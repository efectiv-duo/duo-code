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
            // Check if running in headless mode
            bool isHeadless = Environment.GetCommandLineArgs().Contains("--headless");

            if (isHeadless)
            {
                return ShowNonInteractiveModelSelection(args);
            }
            else
            {
                return ShowInteractiveModelSelection();
            }
        }

        private Task<CommandResult> ShowNonInteractiveModelSelection(string[] args)
        {
            try
            {
                var currentProvider = ApiSettings.CurrentProvider;
                var currentModel = ApiSettings.CurrentModel;

                // If no args provided, show current selection and available models
                if (args.Length == 0)
                {
                    var message = $"Current Model: {currentProvider} - {currentModel}\n\nAvailable Models:\n";
                    var availableModels = GetAllModelsWithDetails();

                    foreach (var model in availableModels)
                    {
                        var isCurrent = model.Provider == currentProvider && model.Model == currentModel;
                        var marker = isCurrent ? " (current)" : "";
                        message += $"- {model.Provider} - {model.Model}{marker}\n";
                    }

                    message += "\nUsage: /model <provider> <model> or /model <provider-model>";
                    return Task.FromResult(CommandResult.Ok(message));
                }

                // Parse arguments
                string newProvider, newModel;

                if (args.Length == 1)
                {
                    // Format: /model provider-model
                    var parts = args[0].Split('-', 2);
                    if (parts.Length != 2)
                    {
                        return Task.FromResult(CommandResult.Error("Invalid format. Use: /model <provider> <model> or /model <provider-model>"));
                    }
                    newProvider = parts[0];
                    newModel = parts[1];
                }
                else if (args.Length == 2)
                {
                    // Format: /model provider model
                    newProvider = args[0];
                    newModel = args[1];
                }
                else
                {
                    return Task.FromResult(CommandResult.Error("Invalid arguments. Use: /model <provider> <model> or /model <provider-model>"));
                }

                // Validate the model exists
                var modelsList = GetAllModelsWithDetails();
                var targetModel = modelsList.FirstOrDefault(m =>
                    string.Equals(m.Provider.ToString(), newProvider, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(m.Model, newModel, StringComparison.OrdinalIgnoreCase));

                if (targetModel == default)
                {
                    return Task.FromResult(CommandResult.Error($"Model '{newProvider}-{newModel}' not found. Use /model to see available options."));
                }

                // Update settings
                ApiSettings.CurrentProvider = targetModel.Provider;
                ApiSettings.CurrentModel = targetModel.Model;

                // Save the settings
                ApiKeyManager.SaveCurrentSettings(ApiSettings.CurrentProvider, ApiSettings.CurrentModel);

                return Task.FromResult(CommandResult.Ok($"Model updated to: {targetModel.Provider} - {targetModel.Model}"));
            }
            catch (Exception ex)
            {
                return Task.FromResult(CommandResult.Error($"Error during model selection: {ex.Message}"));
            }
        }

        private Task<CommandResult> ShowInteractiveModelSelection()
        {
            try
            {
                // Show current selection
                var currentProvider = ApiSettings.CurrentProvider;
                var currentModel = ApiSettings.CurrentModel;
                
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
                ApiSettings.CurrentProvider = selectedModel.Provider;
                ApiSettings.CurrentModel = selectedModel.Model;
                
                // Save the settings
                ApiKeyManager.SaveCurrentSettings(ApiSettings.CurrentProvider, ApiSettings.CurrentModel);

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

            foreach (var provider in ApiSettings.AvailableModels)
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
            var currentProvider = ApiSettings.CurrentProvider;
            var providers = string.Join("\n", Enum.GetNames<ApiProvider>().Select(p => 
                p == currentProvider.ToString() ? $"  {p} (current)" : $"  {p}"));
            var message = $"Available providers:\n{providers}\n\nUsage: /model provider <name>";
            return Task.FromResult(CommandResult.Ok(message));
        }

        private Task<CommandResult> ShowAvailableModels()
        {
            var allModels = GetAllModelsWithNumbers();
            var currentProvider = ApiSettings.CurrentProvider;
            var currentModel = ApiSettings.CurrentModel;
            
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

            foreach (var provider in ApiSettings.AvailableModels)
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