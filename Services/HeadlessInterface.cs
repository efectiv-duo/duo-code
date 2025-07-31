using System.Text.Json;
using duo_code.Models;
using duo_code.Commands.Core;
using duo_code.Services.Interfaces;

namespace duo_code.Services
{
    public class HeadlessInterface : IConsoleInterface
    {
        private readonly List<string> _commandHistory = new();
        private int _historyIndex = -1;
        private CommandRegistry? _commandRegistry;
        private readonly FileSearchService _fileSearchService;
        private readonly object _outputLock = new object();

        public HeadlessInterface()
        {
            _fileSearchService = new FileSearchService();
        }

        public void SetCommandRegistry(CommandRegistry commandRegistry)
        {
            _commandRegistry = commandRegistry;
        }

        // Output methods that send JSON to frontend
        public void ShowWelcomeMessage()
        {
            var welcomeData = new TextOutputData
            {
                Content = "Duo-Code AI Assistant initialized. Type /help for commands or switch modes with Shift+Tab.",
                Style = "info"
            };
            SendOutput(MessageTypes.Text, welcomeData);

            var logger = new ConversationLogger();
            var logInfo = new TextOutputData
            {
                Content = $"Conversation logs saved to: {logger.GetLogsDirectory()}",
                Style = "info"
            };
            SendOutput(MessageTypes.Text, logInfo);
        }

        public void ShowAssistantResponse(string response)
        {
            var data = new AssistantResponseData
            {
                Content = response,
                IsComplete = true
            };
            SendOutput(MessageTypes.AssistantResponse, data);
        }

        public void ShowToolExecution(string toolName)
        {
            var data = new ToolExecutionData
            {
                ToolName = toolName,
                Description = $"Executing {toolName}..."
            };
            SendOutput(MessageTypes.ToolExecution, data);
        }

        public void ShowToolResult(string result)
        {
            var data = new ToolResultData
            {
                ToolName = "",
                Result = result,
                Success = true
            };
            SendOutput(MessageTypes.ToolResult, data);
        }

        public void ShowError(string error)
        {
            var data = new ErrorData
            {
                Message = error,
                ErrorType = "general"
            };
            SendOutput(MessageTypes.Error, data);
        }

        public void ShowInfo(string info)
        {
            var data = new TextOutputData
            {
                Content = info,
                Style = "info"
            };
            SendOutput(MessageTypes.Text, data);
        }

        public void ShowModeSwitch(Mode newMode)
        {
            var color = newMode switch
            {
                Mode.Default => "cyan",
                Mode.Planning => "yellow",
                Mode.Orchestrator => "magenta",
                _ => "white"
            };

            var data = new ModeChangedData
            {
                NewMode = newMode,
                DisplayName = newMode.ToDisplayString(),
                Color = color
            };
            SendOutput(MessageTypes.ModeChanged, data);
        }

        public void ShowThinking()
        {
            var data = new ThinkingData
            {
                IsActive = true
            };
            SendOutput(MessageTypes.Thinking, data);
        }

        public void ClearThinking() 
        {
            var data = new ThinkingData
            {
                IsActive = false
            };
            SendOutput(MessageTypes.Thinking, data);
        }

        public void ShowProgress(string description, Action action)
        {
            var data = new ProgressData
            {
                Description = description,
                IsActive = true
            };
            SendOutput(MessageTypes.Progress, data);
            
            try
            {
                action();
            }
            finally
            {
                var endData = new ProgressData
                {
                    Description = description,
                    IsActive = false
                };
                SendOutput(MessageTypes.Progress, endData);
            }
        }

        public async Task ShowProgressAsync(string description, Func<Task> asyncAction)
        {
            var data = new ProgressData
            {
                Description = description,
                IsActive = true
            };
            SendOutput(MessageTypes.Progress, data);
            
            try
            {
                await asyncAction();
            }
            finally
            {
                var endData = new ProgressData
                {
                    Description = description,
                    IsActive = false
                };
                SendOutput(MessageTypes.Progress, endData);
            }
        }

        public bool WaitForContinueOrCancel()
        {
            var data = new AwaitingApprovalData
            {
                Prompt = "Continue or cancel?",
                Options = new[] { "continue", "cancel" },
                Context = "Operation requires confirmation"
            };
            SendOutput(MessageTypes.AwaitingApproval, data);
            
            // This will be handled asynchronously - the frontend will send back an approval_response
            // For now, return true to continue (this will be properly implemented in the main loop)
            return true;
        }

        // Input handling methods
        public List<FileSuggestion> GetFileSuggestions(string searchTerm, int maxResults = 10)
        {
            // If search term is empty, get all files using a wildcard pattern
            // Otherwise, search for files matching the term
            var searchPattern = string.IsNullOrWhiteSpace(searchTerm) ? "*" : searchTerm;
            var results = _fileSearchService.SearchFiles(searchPattern, Directory.GetCurrentDirectory(), maxResults);
            
            return results.Select(r => new FileSuggestion
            {
                FileName = r.FileName,
                RelativePath = r.RelativePath,
                Directory = Path.GetDirectoryName(r.RelativePath) ?? ""
            }).ToList();
        }

        public List<string> GetCommandCompletions(string commandPrefix)
        {
            if (_commandRegistry == null || string.IsNullOrWhiteSpace(commandPrefix))
                return new List<string>();

            return _commandRegistry.GetAllCommandNames()
                .Where(name => name.StartsWith(commandPrefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(name => name)
                .ToList();
        }

        public string? GetHistoryItem(string direction)
        {
            if (direction == "up")
            {
                return GetPreviousCommand();
            }
            else if (direction == "down")
            {
                return GetNextCommand();
            }
            return null;
        }

        public void AddToHistory(string command)
        {
            if (!string.IsNullOrWhiteSpace(command) && 
                (_commandHistory.Count == 0 || _commandHistory[^1] != command))
            {
                _commandHistory.Add(command);
                if (_commandHistory.Count > 1000)
                {
                    _commandHistory.RemoveAt(0);
                }
            }
            _historyIndex = -1; // Reset history navigation
        }

        public HeadlessSessionState GetSessionState(ConversationState conversationState)
        {
            return new HeadlessSessionState
            {
                CurrentMode = conversationState.CurrentMode,
                CommandHistory = new List<string>(_commandHistory),
                IsRunning = conversationState.IsRunning,
                CurrentModel = conversationState.CurrentModel,
                MessageCount = conversationState.Messages.Count
            };
        }

        private string? GetPreviousCommand()
        {
            if (_commandHistory.Count == 0) return null;

            if (_historyIndex == -1)
            {
                _historyIndex = _commandHistory.Count - 1;
            }
            else if (_historyIndex > 0)
            {
                _historyIndex--;
            }

            return _historyIndex >= 0 ? _commandHistory[_historyIndex] : null;
        }

        private string? GetNextCommand()
        {
            if (_commandHistory.Count == 0 || _historyIndex == -1) return null;

            _historyIndex++;

            if (_historyIndex >= _commandHistory.Count)
            {
                _historyIndex = -1;
                return null;
            }

            return _commandHistory[_historyIndex];
        }

        private void SendOutput(string messageType, object data, string? correlationId = null)
        {
            lock (_outputLock)
            {
                var message = new OutputMessage
                {
                    Type = messageType,
                    Data = data,
                    Id = correlationId
                };

                var json = JsonSerializer.Serialize(message, new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                });
                
                Console.WriteLine(json);
                Console.Out.Flush();
            }
        }

        public void SendSessionState(ConversationState conversationState, string? correlationId = null)
        {
            var sessionState = GetSessionState(conversationState);
            SendOutput(MessageTypes.SessionState, sessionState, correlationId);
        }
    }
}