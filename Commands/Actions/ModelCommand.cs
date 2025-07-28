using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using duo_code.Commands.Core;
using duo_code.Services;

namespace duo_code.Commands.Actions
{
    public class ModelCommand : ICommand
    {
        public string Name => "model";
        public string Description => "Change the AI model and provider";
        public CommandType Type => CommandType.Action;

        public Task<CommandResult> ExecuteAsync(string[] args)
        {
            if (args.Length == 0)
            {
                return ShowAvailableModels();
            }

            if (args[0].ToLower() == "provider")
            {
                if (args.Length < 2)
                {
                    return ShowProviders();
                }

                if (Enum.TryParse<ApiProvider>(args[1], true, out var provider))
                {
                    ApiSettings.CurrentProvider = provider;
                    var models = ApiSettings.AvailableModels[provider];
                    ApiSettings.CurrentModel = models.First();
                    
                    // Save the settings
                    ApiKeyManager.SaveCurrentSettings(ApiSettings.CurrentProvider, ApiSettings.CurrentModel);
                    
                    return Task.FromResult(CommandResult.Ok($"Provider changed to: {provider}\nModel set to: {ApiSettings.CurrentModel}"));
                }

                return Task.FromResult(CommandResult.Error($"Invalid provider. Available: {string.Join(", ", Enum.GetNames<ApiProvider>())}"));
            }

            // Handle model selection by number
            var allModels = GetAllModelsWithNumbers();
            if (allModels.TryGetValue(args[0], out var modelInfo))
            {
                ApiSettings.CurrentProvider = modelInfo.Provider;
                ApiSettings.CurrentModel = modelInfo.Model;
                
                // Save the settings
                ApiKeyManager.SaveCurrentSettings(ApiSettings.CurrentProvider, ApiSettings.CurrentModel);
                
                return Task.FromResult(CommandResult.Ok($"Provider: {modelInfo.Provider}\nModel changed to: {modelInfo.Model}"));
            }

            return Task.FromResult(CommandResult.Error($"Invalid model selection. Use /model to see available models."));
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