using System.Text.Json.Serialization;
using duo_code.Tools.Core;

namespace duo_code.Models
{
    // Input message types (Frontend → Backend)
    public class InputMessage
    {
        public string Type { get; set; } = string.Empty;
        public object? Data { get; set; }
        public string? Id { get; set; } // For request correlation
    }

    public class UserInputData
    {
        public string Content { get; set; } = string.Empty;
    }

    public class CommandData
    {
        public string Command { get; set; } = string.Empty;
        public string[] Args { get; set; } = Array.Empty<string>();
    }

    public class FileSuggestionRequestData
    {
        public string SearchTerm { get; set; } = string.Empty;
        public int CursorPosition { get; set; }
        public string CurrentLine { get; set; } = string.Empty;
    }

    public class ApprovalResponseData
    {
        public string Response { get; set; } = string.Empty; // "yes", "always", "no"
        public string? Feedback { get; set; }
    }

    public class HistoryNavigationData
    {
        public string Direction { get; set; } = string.Empty; // "up", "down"
    }

    // Output message types (Backend → Frontend)
    public class OutputMessage
    {
        public string Type { get; set; } = string.Empty;
        public object? Data { get; set; }
        public string? Id { get; set; } // For request correlation
        public HeadlessSessionState? SessionState { get; set; }
    }

    public class TextOutputData
    {
        public string Content { get; set; } = string.Empty;
        public string Style { get; set; } = "normal"; // "normal", "info", "warning", "success"
    }

    public class AssistantResponseData
    {
        public string Content { get; set; } = string.Empty;
        public string? Thinking { get; set; }
        public bool IsComplete { get; set; } = true;
    }

    public class ToolExecutionData
    {
        public string ToolName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class ToolResultData
    {
        public string ToolName { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
        public bool Success { get; set; } = true;
    }

    public class ErrorData
    {
        public string Message { get; set; } = string.Empty;
        public string? Details { get; set; }
        public string ErrorType { get; set; } = "general";
    }

    public class FileSuggestionsData
    {
        public List<FileSuggestion> Suggestions { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public bool HasMore { get; set; }
        public string SearchTerm { get; set; } = string.Empty;
    }

    public class FileSuggestion
    {
        public string FileName { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public string Directory { get; set; } = string.Empty;
    }

    public class ModeChangedData
    {
        public Mode NewMode { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
    }

    public class AwaitingApprovalData
    {
        public string Prompt { get; set; } = string.Empty;
        public string[] Options { get; set; } = Array.Empty<string>();
        public string Context { get; set; } = string.Empty;
    }

    public class ProgressData
    {
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int? PercentComplete { get; set; }
    }

    public class ThinkingData
    {
        public bool IsActive { get; set; }
        public string? Content { get; set; }
    }

    // Session state for frontend
    public class HeadlessSessionState
    {
        public Mode CurrentMode { get; set; } = Mode.Default;
        public List<string> CommandHistory { get; set; } = new();
        public bool IsRunning { get; set; } = true;
        public string CurrentModel { get; set; } = string.Empty;
        public bool ApprovedThisSession { get; set; } = false;
        public int MessageCount { get; set; }
    }

    // Constants for message types
    public static class MessageTypes
    {
        // Input types
        public const string UserInput = "user_input";
        public const string Command = "command";
        public const string ModeSwitch = "mode_switch";
        public const string FileSuggestionRequest = "file_suggestion_request";
        public const string ApprovalResponse = "approval_response";
        public const string HistoryNavigation = "history_navigation";
        public const string CancelOperation = "cancel_operation";
        public const string Init = "init";

        // Output types
        public const string Text = "text";
        public const string AssistantResponse = "assistant_response";
        public const string ToolExecution = "tool_execution";
        public const string ToolResult = "tool_result";
        public const string Error = "error";
        public const string FileSuggestions = "file_suggestions";
        public const string ModeChanged = "mode_changed";
        public const string AwaitingApproval = "awaiting_approval";
        public const string Progress = "progress";
        public const string Thinking = "thinking";
        public const string SessionState = "session_state";
    }
}