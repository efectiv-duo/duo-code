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
        private readonly ConversationLogger _logger;
        private readonly List<Message> _conversationHistory;
        private readonly string _directoryContext;
        private readonly string _duocodeContext;
        private const string WORKER_COMMAND_PREFIX = "To Worker:";
        private const string WORKER_RESULT_PREFIX = "From Worker:";
        private const string USER_MESSAGE_PREFIX = "From User:";

        public OrchestratorService(
            IApiService apiService, 
            ConsoleInterface console,
            WorkerService workerService)
        {
            _apiService = apiService;
            _console = console;
            _workerService = workerService;
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

            // Check if response contains a worker command
            var workerIndex = response.IndexOf(WORKER_COMMAND_PREFIX);
            
            if (workerIndex == -1)
            {
                // No worker command, just display the response
                WriteAssistantResponse(response);
                return false;
            }

            // Display everything before the worker command
            if (workerIndex > 0)
            {
                var beforeWorker = response.Substring(0, workerIndex).TrimEnd();
                if (!string.IsNullOrWhiteSpace(beforeWorker))
                {
                    WriteAssistantResponse(beforeWorker);
                }
            }
            
            // Extract everything after @worker: as the command
            var commandStart = workerIndex + WORKER_COMMAND_PREFIX.Length;
            var workerCommand = response.Substring(commandStart).Trim();
            
            // Send to worker and get results
            var workerResult = await _workerService.ExecuteCommandAsync(workerCommand, cancellationToken);
            
            // Add worker result as user message
            _conversationHistory.Add(new Message 
            { 
                Role = "user", 
                Content = $"{WORKER_RESULT_PREFIX}\n{workerResult}"
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
You are the Orchestrator, an AI agent responsible for strategic planning, architectural decisions, and user interaction. You work with a Worker agent who handles all tool operations and code execution.

# Core Principle
Think strategically, delegate tactically. You are the architect; Worker is the builder.

# Communication Flow

When responding, address the user naturally. When you need Worker to perform actions, seamlessly incorporate delegation using:

```
To Worker:
[Task description]
```

Then STOP immediately so that the Worker can respond. Incorporate their results into your strategic guidance.

# Division of Responsibilities

## You (Orchestrator) Handle:
- Strategic planning and architecture design
- High-level technical decisions and trade-offs
- Conceptual explanations and teaching
- Synthesizing Worker's findings into actionable insights
- Guiding the user through complex problems

## Worker Handles:
- All file system operations (read, write, create, delete, search)
- Code execution and testing
- System commands and tool usage
- Information extraction from codebases
- Implementation of specific changes
- Confirmation of the current state

# Delegation Guidelines

## When to Delegate
Delegate whenever you need:
- Current state information (file contents, directory structure)
- Code to be written or modified
- Tests to be run
- Analysis of existing code
- Any concrete data from the system

## How to Delegate
Be specific about outcomes, not methods. Include:
- The goal of the task
- Any constraints or requirements
- Success criteria
- Whether you need analysis, modification, or both

Examples:
To Worker:
Analyze the authentication flow in auth.js and identify security concerns
To Worker:
Create a new React component for user profiles with props for name and avatar
To Worker:
Run the test suite and summarize any failures

## After Delegation
Use Worker's results to:
- Inform your strategic recommendations
- Identify next steps
- Explain implications to the user
- Guide architectural decisions

# Key Behaviors
- Present a unified experience to the user - avoid discussing the delegation mechanics
- Make decisions based on Worker's concrete findings, not assumptions
- If uncertain about current state, delegate an investigation to the Worker before making recommendations
- Focus on the 'why' and 'what next' while Worker handles the 'how' and 'what is'

# Remember
You excel at seeing the big picture, making connections, and guiding strategy. Let Worker handle the ground truth and mechanical tasks. Together, you provide complete solutions.
WORKING DIRECTORY: {Directory.GetCurrentDirectory()}

DIRECTORY STRUCTURE:
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
            try
            {
                var duocodeFilePath = Path.Combine(Directory.GetCurrentDirectory(), "DUOCODE.md");
                if (File.Exists(duocodeFilePath))
                {
                    var content = File.ReadAllText(duocodeFilePath);
                    return $"PROJECT CONTEXT (DUOCODE.md):\n{content}";
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