# DUOCODE Project Documentation

This document provides a comprehensive overview of the DUOCODE project, designed to assist future AI assistants in understanding, maintaining, and extending the codebase.

## 1. Project Overview

DUOCODE is a console-based AI assistant designed to help developers with various coding tasks. It acts as an interactive agent that can understand natural language commands, execute tools (like file operations, command execution, and code search), interact with large language models (LLMs) from different providers (e.g., Cerebras, Gemini), and potentially solve coding challenges. Its primary purpose is to streamline development workflows by providing an intelligent, interactive coding companion.

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

DUOCODE is built as a console application using .NET 8.0. Its architecture is centered around a command-line interface (CLI) and an AI agent that orchestrates interactions.

*   **Command Pattern**: User inputs are parsed into commands, which are then executed. `ICommand` defines the contract for executable commands, and `CommandRegistry` manages their discovery and invocation.
*   **Tooling System**: The AI agent interacts with the environment through a set of predefined "tools." Each tool is an `IToolAction` implementation, allowing the agent to perform actions like reading/writing files, running shell commands, and searching the codebase. `ToolRegistry` manages available tools.
*   **Service-Oriented Design**: Core functionalities (like API interactions, configuration, conversation state management) are encapsulated within dedicated services.
*   **LLM Integration**: The application abstracts interactions with different Large Language Models (LLMs) through an `IApiService` interface. Concrete implementations exist for providers like Cerebras and Gemini. Streaming responses from LLMs are handled by `StreamingResponseService`.
*   **Challenges**: A separate module for defining and running AI-driven coding challenges, allowing the agent to practice and demonstrate its capabilities.

The `Program.cs` acts as the bootstrap, initializing core services, command, and tool registries, and then handing control to the `AgentService` for the main interaction loop.

## 4. Key Components

*   **`Program.cs`**: The application's entry point. It handles initial setup, API key management, service registration (CommandRegistry, ToolRegistry, AgentService, etc.), and determines whether to run in interactive mode or as a subagent.
*   **`Services/AgentService.cs`**: The central orchestrator of the AI assistant. It manages the conversation flow, processes user input, invokes commands, interacts with LLMs, and executes tools based on AI responses.
*   **`Commands/Core/CommandRegistry.cs`**: Discovers and registers all available user commands (implementing `ICommand`). It's responsible for mapping user input to the correct command execution logic.
*   **`Tools/Core/ToolRegistry.cs`**: Discovers and registers all available tools (implementing `IToolAction`). It provides a mechanism for the `AgentService` to invoke specific system actions requested by the AI.
*   **`Services/ApiServiceFactory.cs` & `Services/*ApiService.cs`**: Handles the creation and management of API service instances for different LLM providers (e.g., Cerebras, Gemini). It abstracts the underlying API calls.
*   **`Services/StreamingResponseService.cs`**: Manages the processing of streaming responses from LLMs, handling partial data and displaying it to the console.
*   **`Services/ConversationState.cs`**: Maintains the current state of the conversation, including the chat history, current working directory, and selected AI model.
*   **`Services/Configuration/ConfigurationService.cs` & `Services/ApiKeyManager.cs`**: Manages application settings loaded from `AppSettings.json` (if implemented) and securely handles API keys.
*   **`Challenges/Core/ChallengeRegistry.cs`**: Manages the discovery and execution of various coding challenges that the agent can attempt.

## 5. Dependencies

The project relies on the following external NuGet packages:

*   **`Newtonsoft.Json` (Version 13.0.3)**: A popular high-performance JSON framework for .NET. Used for serializing and deserializing data, especially for communication with LLM APIs and managing configuration.
*   **`GitignoreParserNet` (Version 0.2.0.14)**: A utility for parsing `.gitignore` files. Likely used by tools to determine which files should be ignored during operations like searching or context building.
*   **`TextDiff.Sharp` (Version 1.0.3)**: A library for generating unified diffs between text. Used by the `UpdateFileAction` tool to apply patch-like changes to files.

## 6. Development Guidelines

*   **Language**: C# 12, .NET 8.0.
*   **Coding Standards**: Adhere to standard C# coding conventions and best practices (e.g., PascalCase for types and members, camelCase for local variables, meaningful names, use of `async`/`await` for asynchronous operations).
*   **Modularity**: Keep components loosely coupled and highly cohesive. New features should ideally fit into existing service, command, or tool patterns.
*   **Error Handling**: Implement robust error handling, especially for external API calls and file system operations.
*   **Console Output**: Use `ConsoleInterface` for all console interactions to maintain consistency and allow for potential future abstraction.
*   **Configuration**: All configurable parameters should be managed through `ConfigurationService` or `ApiKeyManager`, avoiding hardcoded values.

## 7. Build and Test

The project uses the standard .NET CLI for building and running.

*   **Build**:
    ```bash
    dotnet build
    ```
*   **Run**:
    ```bash
    dotnet run
    ```
    To run as a subagent with a specific prompt:
    ```bash
    dotnet run -- --subagent "your subagent prompt here"
    ```
    To specify an API key on startup:
    ```bash
    dotnet run -- --api-key=YOUR_API_KEY
    ```
*   **Test**: (Currently, no dedicated test project is present. If tests were added, they would be run with):
    ```bash
    dotnet test
    ```

## 8. Common Tasks

*   **Starting the Agent**: Run `dotnet run` from the project root.
*   **Setting API Key**: The agent will prompt for an API key on first run if not provided via command line. The key is saved locally.
*   **Changing AI Model/Provider**: Use the `model` command within the agent's console (e.g., `model cerebras-7b` or `model gemini-pro`).
*   **Getting Help**: Type `help` in the agent's console to see available commands.
*   **Exiting**: Type `exit` or `quit` in the agent's console.
*   **Clearing Console**: Type `clear` in the agent's console.

## 9. Known Issues

*   **Hardcoded Directory**: In `Program.cs`, there is a temporary hardcoded `Directory.SetCurrentDirectory` line (around line 31). This should be removed before release to allow the application to run from its execution directory or a user-specified directory.
*   **No Unit Tests**: As of this documentation, there is no dedicated unit test project. Adding comprehensive tests is recommended for future development.
*   **Limited Error Reporting**: While basic error handling is present, more detailed logging and user-friendly error messages could be implemented for a better experience.