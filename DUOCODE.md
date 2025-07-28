# DUOCODE Project Documentation

## 1. Project Overview
A .NET 8 coding agent designed to assist developers with challenge-based problem solving and code automation tasks. The system acts as an intelligent assistant that can process commands, execute challenges, and utilize various tools to perform development operations.

## 2. Project Structure
```
/ (root)
├── DUOCODE.md
├── Program.cs
├── duo-code.csproj
├── duo-code.sln
├── Challenges/
│   ├── Categories/
│   └── Core/
├── Commands/
│   ├── Actions/
│   ├── Core/
│   └── Prompts/
├── Models/
├── Services/
│   └── Configuration/
└── Tools/
    ├── Core/
    └── CreateFileAction.cs
```

## 3. Architecture
Modular component-based architecture with:
- Command processing pipeline
- Tool execution framework
- Streaming response system
- Configuration management
- API integration layer

## 5. Dependencies
- .NET 8 SDK
- System.IO for file operations
- Cerebras API integration
- Custom configuration management

## 6. Development Guidelines
- Tool implementations inherit from ToolActionBase
- All tools must override ExecuteCore method
- Path resolution uses base directory context
- Follow strict format for tool commands

## 7. Build and Test
# Build project
dotnet build

# Run project
dotnet run

## 9. Known Issues
- Limited error handling in file operations
- Path resolution requires careful handling of relative paths
- Tool command format must be strictly followed
