using duo_code.Commands.Core;
using duo_code.Models;
using duo_code.Services.Interfaces;
using duo_code.Tools;
using static duo_code.Utils.ConsoleHelper;

namespace duo_code.Services
{
    public class OrchestratorService
    {
        private readonly IApiService _apiService;
        private readonly ConsoleInterface _console;
        private readonly WorkerService _workerService;
        private readonly ObserverService _observerService;
        private readonly ConversationLogger _logger;
        private readonly List<Message> _conversationHistory;
        private readonly string _directoryContext;
        private readonly string _duocodeContext;
        private const string OBSERVER_COMMAND_PREFIX = "@context";
        private const string WORKER_COMMAND_PREFIX = "@solution";
        private const string WORKER_RESULT_PREFIX = "Solution Result:";
        private const string USER_MESSAGE_PREFIX = "From User:";

        public OrchestratorService(
            IApiService apiService, 
            ConsoleInterface console,
            WorkerService workerService,
            ObserverService observerService)
        {
            _apiService = apiService;
            _console = console;
            _workerService = workerService;
            _observerService = observerService;
            _logger = new ConversationLogger();
            _conversationHistory = new List<Message>();
            
            // Build context at initialization
            _directoryContext = BuildDirectoryContext();
            _duocodeContext = BuildDuocodeContext();
        }

        public async Task<bool> ProcessUserMessageAsync(string userInput, CancellationToken cancellationToken = default)
        {
            // Add prefix to user message
            userInput = $"{USER_MESSAGE_PREFIX}\n{userInput}";

            // Add user message to history
            _conversationHistory.Add(new Message { Role = "user", Content = userInput });

            bool continueProcessing = true;
            while (continueProcessing)
            {
                // Build messages for orchestrator
                var messages = BuildOrchestratorMessages();
                
                try
                {
                    WriteInfo("Thinking ...");
                    
                    // Get orchestrator response
                    var response = await _apiService.GetAISuggestionAsync(
                        messages.Select(m => new CerebrasMessage 
                        { 
                            Role = m.Role ?? "user", 
                            Content = m.Content 
                        }).ToList(),
                        cancellationToken,
                        CurrentState.Model,
                        _console
                    );

                    if (string.IsNullOrWhiteSpace(response?.Content))
                    {
                        continueProcessing = false;
                        continue;
                    }

                    // Log orchestrator conversation
                    var orchestratorLog = string.Join("\n\n", messages.Select(m => $"{m.Role?.ToUpper()}:\n{m.Content}")) 
                        + $"\n\nASSISTANT:\n{response.Content}";
                    _logger.SaveConversation(orchestratorLog, "orchestrator");

                    // Process orchestrator response
                    continueProcessing = await ProcessOrchestratorResponse(response.Content, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    _console.ShowInfo("\nOperation cancelled.");
                    return false;
                }
            }

            return true;
        }

        private async Task<bool> ProcessOrchestratorResponse(string response, CancellationToken cancellationToken)
        {
            // Add response to history
            _conversationHistory.Add(new Message 
            { 
                Role = "assistant", 
                Content = response
            });

            // Check if response contains a command
            var observerIndex = response.IndexOf(OBSERVER_COMMAND_PREFIX);
            var workerIndex = -1;

            WriteAssistantResponse(response);

            if (workerIndex == -1 && observerIndex == -1)
            {
                // No command, just display the response
                return false;
            }
            
            // Extract the command
            var command = response;

            var result = "";

            // Get results
            if(observerIndex != -1)
            {
                result = await _observerService.ExecuteCommandAsync(command, cancellationToken);
            }
            else
            {
                result = await _workerService.ExecuteCommandAsync(command, cancellationToken);
            }

            // Add worker result as user message
            _conversationHistory.Add(new Message 
            { 
                Role = "user", 
                Content = $"{result}"
            });

            // Continue processing to handle the worker results
            return true;
        }

        private List<Message> BuildOrchestratorMessages()
        {
            var messages = new List<Message>();
            
            // Add system prompt for orchestrator
            messages.Add(new Message 
            { 
                Role = "system", 
                Content = GetOrchestratorSystemPrompt() 
            });

            // Add conversation history
            messages.AddRange(_conversationHistory);

            return messages;
        }

        private string GetOrchestratorSystemPrompt()
        {
            var prompt = $@"# Role
You are an expert programmer.
Respond with solutions.
Ask the user for context if needed.
Add @context at the end of your response if you are asking for context.
Do not respond with a solution until you have the needed context.
Add @solution at the end of your response if you have provided the solution.
Do not offer unprompted assistance or meta text.
Do not make any assumptions, ask for context.
Be concise.

# Working Directory
{Directory.GetCurrentDirectory()}

# Directory Structure:
{_directoryContext}

{_duocodeContext}";
            
            return prompt;
        }

        private string BuildDirectoryContext()
        {
            try
            {
                var listFilesAction = new ListFilesAction
                {
                    Path = ".",
                    Depth = 3,
                    DirectoriesOnly = false
                };
                return listFilesAction.Execute(Directory.GetCurrentDirectory());
            }
            catch
            {
                return "Unable to retrieve directory structure";
            }
        }

        private string BuildDuocodeContext()
        {
            return "";
            try
            {
                var duocodeFilePath = Path.Combine(Directory.GetCurrentDirectory(), "DUOCODE.md");
                if (File.Exists(duocodeFilePath))
                {
                    var content = File.ReadAllText(duocodeFilePath);
                    return $"# Project Context\n{content}";
                }
                return "";
            }
            catch
            {
                return "";
            }
        }
    }
}