using duo_code.Models;
using duo_code.Services.Interfaces;
using duo_code.Tools;
using duo_code.Tools.Core;

namespace duo_code.Services
{
    public class WorkerService
    {
        private readonly IApiService _apiService;
        private readonly ToolRegistry _toolRegistry;
        private readonly ConsoleInterface _console;
        private readonly List<Message> _workerHistory;
        private readonly string _contextualPrompt;
        private readonly ConversationLogger _logger;

        public WorkerService(
            IApiService apiService,
            ToolRegistry toolRegistry,
            ConsoleInterface console)
        {
            _apiService = apiService;
            _toolRegistry = toolRegistry;
            _console = console;
            _workerHistory = new List<Message>();
            _contextualPrompt = BuildContextualPrompt();
            _logger = new ConversationLogger();
        }

        public async Task<string> ExecuteCommandAsync(string naturalLanguageCommand, CancellationToken cancellationToken = default)
        {
            // Clear history for new task
            _workerHistory.Clear();
            
            // Add initial task
            _workerHistory.Add(new Message 
            { 
                Role = "user", 
                Content = naturalLanguageCommand 
            });
            
            try
            {
                // Loop until worker responds without tools
                while (true)
                {
                    // Build messages for this iteration
                    var messages = BuildWorkerMessages();
                    
                    // Get worker response
                    var response = await _apiService.GetAISuggestionAsync(
                        messages.Select(m => new CerebrasMessage 
                        { 
                            Role = m.Role ?? "user", 
                            Content = m.Content 
                        }).ToList(),
                        cancellationToken,
                        CurrentState.Model,
                        null // Don't pass console for worker
                    );

                    if (string.IsNullOrWhiteSpace(response?.Content))
                    {
                        return "No response from worker";
                    }

                    // Log worker conversation
                    var workerLog = string.Join("\n\n", messages.Select(m => $"{m.Role?.ToUpper()}:\n{m.Content}")) 
                        + $"\n\nASSISTANT:\n{response.Content}";
                    _logger.SaveConversation(workerLog, "worker");

                    // Add assistant response to history
                    _workerHistory.Add(new Message 
                    { 
                        Role = "assistant", 
                        Content = response.Content 
                    });

                    // Parse and check for tools
                    var tools = ToolFactory.Parse(response.Content);
                    
                    if (tools.Count == 0)
                    {
                        // No tools - this is the final summary, return it
                        return response.Content;
                    }

                    // Execute tools and get results
                    var toolResults = ExecuteTools(tools);
                    
                    // Add tool results to history for next iteration
                    _workerHistory.Add(new Message 
                    { 
                        Role = "user", 
                        Content = toolResults 
                    });

                    // Keep history manageable
                    if (_workerHistory.Count > 50)
                    {
                        // Keep system message and recent history
                        var systemMessage = _workerHistory.FirstOrDefault(m => m.Role == "system");
                        var recentHistory = _workerHistory.Skip(Math.Max(0, _workerHistory.Count - 30)).ToList();
                        _workerHistory.Clear();
                        if (systemMessage != null) _workerHistory.Add(systemMessage);
                        _workerHistory.AddRange(recentHistory);
                    }
                }
            }
            catch (Exception ex)
            {
                return $"Worker error: {ex.Message}";
            }
        }

        private string ExecuteTools(List<IToolAction> tools)
        {
            var results = new List<string>();

            foreach (var tool in tools)
            {
                try
                {
                    // Execute tool directly without confirmation
                    tool.Execute(Directory.GetCurrentDirectory());
                    
                    // Collect result
                    if (!string.IsNullOrEmpty(tool.ResultMessage))
                    {
                        results.Add(tool.ResultMessage);
                    }
                    else if (!string.IsNullOrEmpty(tool.ConsoleResultMessage))
                    {
                        results.Add(tool.ConsoleResultMessage);
                    }
                    else
                    {
                        results.Add($"{tool.ToolName} executed successfully");
                    }
                }
                catch (Exception ex)
                {
                    results.Add($"Error executing {tool.ToolName}: {ex.Message}");
                }
            }

            return string.Join("\n", results);
        }

        private List<Message> BuildWorkerMessages()
        {
            var messages = new List<Message>();
            
            // Add system prompt for worker
            messages.Add(new Message 
            { 
                Role = "system", 
                Content = GetWorkerSystemPrompt() 
            });

            // Add all worker history
            messages.AddRange(_workerHistory);

            return messages;
        }

        private string GetWorkerSystemPrompt()
        {
            return _contextualPrompt;
        }

        private string BuildContextualPrompt()
        {
            // Get tool descriptions from registry
            var toolInstructions = _toolRegistry.GetAllToolInstructions();
            
            // Build directory context
            var directoryContext = "";
            try
            {
                var listFilesAction = new ListFilesAction
                {
                    Path = ".",
                    Depth = 3,
                    DirectoriesOnly = false
                };
                directoryContext = listFilesAction.Execute(Directory.GetCurrentDirectory());
            }
            catch
            {
                directoryContext = "Unable to retrieve directory structure";
            }

            return $@"# Role
You are the Worker, a focused task execution agent. You receive instructions from the Orchestrator and complete concrete tasks using available tools.

# Core Behavior
When given a task, execute it completely using your tools in sequence. Continue using tools until the task succeeds, fails definitively, or requires external input.

Your response pattern:
1. Tool calls to complete the task
2. When task complete, provide a natural summary of what you accomplished
3. Include relevant details the Orchestrator needs for next decisions

## Execution Principles
- Handle routine decisions autonomously (file names, common patterns, sensible defaults)
- Only stop for genuinely ambiguous situations requiring clarification
- Attempt obvious error recovery before reporting failures
- Work through multi-step processes without requesting permission for each step

## Response Style
After completing your tool sequence, summarize in natural language:
- What you did
- Key results or findings
- Any relevant context for next steps
- Problems encountered and how resolved (or why blocked)

# Mindset
You are the hands-on implementer. Focus deeply on the specific task at hand. Trust that the Orchestrator will handle the bigger picture - your job is flawless execution and clear reporting.

Work autonomously, think practically, communicate results clearly.

WORKING DIRECTORY: {Directory.GetCurrentDirectory()}

CURRENT DIRECTORY STRUCTURE:
{directoryContext}

# Tools:
{toolInstructions}";
        }
    }
}