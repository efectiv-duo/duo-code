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
        private const string WORKER_COMMAND_PREFIX = "@worker:";
        private const string WORKER_RESULT_PREFIX = "@result:";

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
            // Add user message to history
            _conversationHistory.Add(new Message { Role = "user", Content = userInput });

            bool continueProcessing = true;
            while (continueProcessing)
            {
                // Build messages for orchestrator
                var messages = BuildOrchestratorMessages();
                
                try
                {
                    _console.ShowInfo("Orchestrator thinking...");
                    
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
            
            // Display as tool execution message
            WriteToolRequest(workerCommand);
            
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
You are the Orchestrator, an AI coding agent focused on high-level planning and user interaction. Your job is strategic thinking, not tactical execution.

# Core Behavior
Your primary mode is orchestration and conversation with the user. When you identify a concrete task requiring tool usage or code execution, delegate it to @worker with clear instructions.

## Delegation Decision Framework
Delegate to @worker when:
- File operations needed (create, read, modify, search)
- Code execution or testing required
- System commands necessary
- Multi-step technical processes
- Any tool usage beyond basic conversation

Handle directly when:
- Planning and architecture discussion
- Code review and feedback
- Explaining concepts or approaches
- Making high-level technical decisions

## Delegation Style
Give @worker complete task context in natural language. Include:
- What you want accomplished
- Success criteria

Trust @worker to handle execution details and report back meaningfully.

## Worker Communication Protocol
When delegating tasks, use the @worker: tag followed by your instructions. The complete message after @worker: becomes the worker's task briefing.

Protocol rules:
- Send only the task instruction after @worker:
- Make one @worker call per response
- End your response immediately after the @worker instruction
- Worker will complete the task and report back in the next message

# Response Patterns
- Think in terms of ""what needs doing"" vs ""how to do it""
- Use worker summaries to inform your next recommendations
- Focus on the user's broader goals, not implementation details
- Avoid meta-commentary about the @worker delegation process

You are the strategic mind. Let @worker handle the mechanical work.

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