# DUOCODE Project Documentation

This document provides a comprehensive overview of the Duo-Code project, designed to assist future AI assistants in understanding, maintaining, and developing the codebase.

## 1. Project Overview

Duo-Code is an interactive AI agent application designed to assist users with coding and development tasks. It operates as a command-line interface (CLI) tool, capable of interacting with various AI models (e.g., Cerebras, Gemini) to process user requests, execute system commands, and perform file operations. The agent can run in an interactive mode, engaging in a continuous conversation, or in a subagent mode, processing a single prompt and exiting. Its core purpose is to automate and streamline development workflows by leveraging AI capabilities.

## 2. Project Structure

The project follows a modular structure, organizing code into logical directories based on functionality.

```
.
├── Challenges/                 # Definitions and logic for various AI coding challenges
│   ├── Core/                   # Core interfaces and base classes for challenges
│   └── *.cs                    # Specific challenge implementations (e.g., DebugAndFixChallenge)
├── Commands/                   # User-facing commands that the agent can execute
│   ├── Actions/                # Implementations of specific commands (e.g., help, exit, model)
│   ├── Core/                   # Core interfaces and registry for commands
│   └── Prompts/                # Markdown files containing prompts for command-related actions
├── Models/                     # Data models and DTOs used throughout the application
├── Services/                   # Business logic, external API integrations, and core utilities
│   ├── Configuration/          # Application settings and configuration management
│   ├── Interfaces/             # Interfaces for services
│   └── *.cs                    # Service implementations (e.g., AgentService, ApiService, ConsoleInterface)
├── Tools/                      # Actions the AI agent can perform on the system
│   ├── Core/                   # Core interfaces, base classes, and registry for tools
│   └── *.cs                    # Specific tool implementations (e.g., CreateFileAction, RunCommandAction)
├── Workflows/                  # Definitions for multi-step AI workflows (e.g., default-workflow.md)
├── Program.cs                  # Application entry point and dependency initialization
├── duo-code.csproj             # Project file (C# project configuration, dependencies)
├── duo-code.sln                # Solution file (Visual Studio solution)
└── DUOCODE.md                  # This documentation file
```

## 3. Architecture

The Duo-Code application follows a layered architecture with a strong emphasis on separation of concerns and extensibility.

*   **Core Orchestration**: `Program.cs` serves as the application's entry point, initializing core services and the `AgentService`. The `AgentService` acts as the central orchestrator, managing the conversation flow, command processing, AI interactions, and tool execution.
*   **Command Pattern**: Internal user commands (e.g., `/exit`, `/model`) are handled via a `CommandRegistry` and `ICommand` interface, allowing for easy addition of new commands.
*   **Tool Pattern**: External actions that the AI can perform (e.g., `CREATE_FILE`, `LIST_FILES`) are implemented as `IToolAction` objects and managed by a `ToolRegistry`. The AI's responses are parsed to identify and execute these tools.
*   **Service Layer**: The `Services` directory contains the business logic, including API communication (`ApiService`, `ApiServiceFactory`), console interactions (`ConsoleInterface`), file operations (`FileReferenceService`, `FileSearchService`), and context building (`ContextBuilder`).
*   **State Management**: `CurrentState` provides a centralized, accessible location for global application state, such as conversation history, current AI model, and operational mode.
*   **Dependency Injection**: Services and components are typically wired up in `Program.cs` using constructor injection, promoting loose coupling and testability.
*   **AI Integration**: The system abstracts different AI providers through the `IApiService` interface, allowing for flexible switching between models (e.g., Cerebras, Gemini). Streaming responses are handled to provide a dynamic user experience.

## 4. Key Components

*   **`AgentService.cs`**: The heart of the application. It manages the main interactive loop, processes user input (commands or natural language), builds message history for the AI, sends requests to the `ApiService`, and processes AI responses, including executing detected tools. It also handles subagent mode.
*   **`ConsoleInterface.cs`**: Responsible for all console-based input and output. It provides methods for displaying welcome messages, user prompts, information, errors, and tool results. It also manages user input, including mode switching.
*   **`CommandRegistry` (and `Commands/` directory)**: Manages a collection of `ICommand` implementations. Commands are internal actions triggered by user input starting with `/`.
*   **`ToolRegistry` (and `Tools/` directory)**: Manages a collection of `IToolAction` implementations. Tools are external actions that the AI can instruct the agent to perform (e.g., file system operations, running commands).
*   **`ApiService.cs` (and implementations like `GeminiApiService.cs`, `CerebrasApiService.cs`)**: Defines the interface for interacting with various AI models. Implementations handle specific API calls, request/response formats, and streaming.
*   **`CurrentState.cs`**: A static class holding the mutable state of the application, including the conversation history (`Messages`), current AI provider and model, and the agent's operational mode.
*   **`FileReferenceService.cs`**: Scans user input for file references (e.g., `[FILE:path/to/file.cs]`), reads their content, and injects them into the AI's system message for context.
*   **`ContextBuilder.cs`**: Dynamically builds the initial system messages provided to the AI, including project context, available tools, and current operational mode.

## 5. Dependencies

The project relies on the following key external NuGet packages:

*   **`GitignoreParserNet`**: Used for parsing `.gitignore` files, likely to exclude certain files/directories from file search or context building operations.
*   **`Newtonsoft.Json`**: A popular high-performance JSON framework for .NET, used for serializing and deserializing data, especially for API requests and responses.
*   **`Spectre.Console`**: Provides a rich library for creating beautiful console applications, used extensively for enhanced user interface elements, styling, and interactive prompts.
*   **`TextDiff.Sharp`**: A library for generating and applying text differences (diffs), likely used by the `UpdateFileAction` tool to apply patch-like changes to files.

## 6. Development Guidelines

The project adheres to strict coding practices documented in `CODING_PRACTICES.md`. Key principles include:

*   **Simplicity and Directness**: Write clear, concise code.
*   **Early Returns/Guard Clauses**: Reduce nesting and improve readability.
*   **No Emojis, No Decorations**: Maintain a professional and clean code/message style.
*   **Comments**: Use `//` comments only, explaining *why* something is done, not *what*.
*   **Code Organization**: One statement per line, minimal nesting, logical grouping of files.
*   **Error Handling**: Simple, direct error messages; fail-fast approach.
*   **Async Patterns**: Use `ConfigureAwait(false)` in libraries, support `CancellationToken`.
*   **Naming**: Clear, descriptive names; boolean names as questions (e.g., `IsValid`).
*   **Dependencies**: Constructor injection only, minimal dependencies.
*   **File Structure**: Logical grouping, one class per file (matching file name).
*   **Performance**: Optimize only when necessary, use appropriate data structures.
*   **Security**: Validate all inputs, never log secrets.
*   **Testing**: Test names describe behavior, follow Arrange-Act-Assert pattern.
*   **Console Output**: Consistent, minimal output; use colors for status/errors.
*   **Tool/Command Patterns**: Follow the defined patterns for implementing new tools and commands.

Refer to `CODING_PRACTICES.md` for detailed examples and further guidelines.

## 7. Build and Test

The project uses the standard .NET CLI for building and running.

*   **Build**: To build the project, navigate to the project root directory (`duo-code/`) and run:
    `dotnet build`
*   **Run**: To run the application in interactive mode:
    `dotnet run`
    To run in subagent mode with a specific prompt:
    `dotnet run -- --subagent "Your prompt here"`
*   **Test**: (Assuming unit tests exist, though not explicitly listed in initial directory analysis) To run tests:
    `dotnet test`

## 8. Common Tasks

*   **Start Interactive Session**:
    `dotnet run`
*   **Run as Subagent**:
    `dotnet run -- --subagent "Refactor the 'Utils/ArgumentParser.cs' file to improve readability."`
*   **Switch AI Model**: In interactive mode, type `/model <model_name>` (e.g., `/model gemini-pro`).
*   **Exit Application**: In interactive mode, type `/exit`.
*   **Clear Conversation History**: In interactive mode, type `/clear`.
*   **Get Help**: In interactive mode, type `/help`.
*   **Reference Files in Prompt**: Use the `[FILE:path/to/file.cs]` syntax in your user input to include file content in the AI's context.

## 9. Known Issues

*   **Current Directory Hardcoding**: In `Program.cs`, the current directory is hardcoded (Line 17: `Directory.SetCurrentDirectory("C:\\Work\\Efectiv Duo\\projects\\duo-code\\");`). This should be made dynamic or configurable for better portability.
*   **Tool Execution Error Handling**: While tools have `try-catch` blocks, the overall error handling for tool execution might need more robust mechanisms to prevent agent loops or provide more actionable feedback to the AI.
*   **Context Window Management**: For very long conversations or large file references, the AI's context window might become a limitation. Strategies for summarization or intelligent context pruning may be needed.
*   **User Input Cancellation**: The `_console.WaitForContinueOrCancel()` is currently commented out in `AgentService.cs` (lines 241-248), meaning the agent proceeds without explicit user confirmation after tool execution. This might be desired behavior but note the change.