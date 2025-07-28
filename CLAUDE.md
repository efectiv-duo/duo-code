# Duo-Code Project Documentation

## Overview
Duo-Code is a .NET 8.0 console application that provides an AI-powered command-line interface for code assistance. It integrates with the Cerebras API to provide intelligent suggestions and supports a modular tool system for various code operations.

## Project Structure
```
duo-code/
├── Commands/
│   ├── Actions/          # Action commands (executed by CLI)
│   │   └── ModelCommand.cs
│   ├── Core/            # Command infrastructure
│   │   ├── ICommand.cs
│   │   ├── CommandResult.cs
│   │   ├── CommandType.cs
│   │   └── CommandRegistry.cs
│   └── Prompts/         # Prompt commands (loaded from .md files)
│       └── init.md
├── Models/              # Data models
│   ├── Message.cs
│   ├── CodebaseContext.cs
│   ├── FileMetadata.cs
│   └── ChatCompletionChunk.cs
├── Services/            # Business logic and services
│   ├── ApiKeyManager.cs
│   ├── CodebaseContextFactory.cs
│   ├── StreamingResponseProcessor.cs
│   └── CerebrasApiService.cs
├── Tools/               # Individual tool implementations
│   ├── Core/           # Tool infrastructure
│   │   ├── IToolAction.cs
│   │   ├── ToolFactory.cs
│   │   └── ToolRegistry.cs
│   ├── AnalyzeDirectoryAction.cs
│   ├── CreateDirectoryAction.cs
│   ├── CreateFileAction.cs
│   ├── DeleteDirectoryAction.cs
│   ├── DeleteFileAction.cs
│   ├── ListFilesAction.cs
│   ├── MoveDirectoryAction.cs
│   ├── MoveFileAction.cs
│   ├── ReadFileAction.cs
│   └── UpdateFileAction.cs
├── Program.cs           # Main entry point
└── duo-code.csproj     # Project configuration

```

## Key Components

### 1. Command System
- **Action Commands**: Direct CLI-executed commands (e.g., `/model`)
- **Prompt Commands**: Commands loaded from .md files that submit prompts to the AI
- Commands are dynamically discovered and registered at startup

### 2. Tool System
Each tool implements `IToolAction` with:
- `ToolName`: Identifier used in AI responses
- `Description`: Verbatim string describing tool usage for AI
- `Execute()`: Implementation of the tool's functionality

### 3. Services
- **CerebrasApiService**: Handles API communication with streaming support
- **StreamingResponseProcessor**: Processes streaming responses with thinking block handling
  - Preserves both full response (with `<think>` blocks) and clean response
  - Shows "Thinking..." indicator during thinking blocks
  - Returns ProcessedResponse with both versions
- **CodebaseContextFactory**: Analyzes project structure and metadata
- **ApiKeyManager**: Manages API key storage and retrieval

### 4. Color Scheme
- Yellow: All non-user messages and AI responses
- Cyan: Tool outputs
- Red: Error messages

## Build Instructions
```bash
dotnet build
dotnet run
```

## Configuration
- API key is stored in `~/.cerebras-api-key`
- Available models can be switched using `/model` command
- Prompt commands can be added by creating .md files in Commands/Prompts/

## Tool Usage Format
Tools follow a specific format in AI responses:
```
TOOL_NAME: parameter
additional_content_if_needed
```

## Adding New Features

### Adding a New Tool
1. Create a new class in Tools/ implementing IToolAction
2. Define ToolName and Description using verbatim strings
3. Implement Execute() method
4. Tool will be automatically discovered and registered

### Adding a New Command
- **Action Command**: Create class in Commands/Actions/ implementing ICommand
- **Prompt Command**: Create .md file in Commands/Prompts/

## Architecture Decisions
- Uses static methods for simplicity (avoids complex DI)
- Factory pattern for tool creation
- Registry pattern for dynamic command/tool discovery
- Streaming response processing for better UX
- Modular structure for maintainability

## Thinking Block Support
The application handles AI thinking blocks (`<think>...</think>`):
- **Current Turn**: Includes thinking blocks in conversation history
- **Past Turns**: Only clean responses (without thinking) are sent to AI
- **Display**: Shows "Thinking..." indicator, displays only clean response
- **Storage**: Both full and clean versions are tracked in Message model

## Current Cerebras Models
1. qwen-3-235b-a22b (default)
2. qwen-3-32b
3. llama-4-maverick-17b-128e-instruct
4. llama-4-scout-17b-16e-instruct
5. deepseek-r1-distill-llama-70b

## Development Notes
- .NET 8.0 required
- MSBuild configured to copy .md files to output directory
- Path resolution checks multiple locations for prompt commands
- Console application with simple command loop