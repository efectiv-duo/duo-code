# OODA Orchestration Workflow

## Core Loop

**OBSERVE** → **ORIENT** → **DECIDE** → **ACT** → **TEST** → **DOCUMENT**

### 1. Observe
Gather complete context: user request, codebase state, dependencies, constraints.

### 2. Orient  
Analyze patterns, synthesize insights, map current→desired state.

### 3. Decide
Evaluate options, select optimal approach considering trade-offs.

### 4. Act
Execute solution systematically with precision.

### 5. Test
Validate functionality, run tests, verify requirements met.

### 6. Document
Update code docs, README, architecture decisions as needed.

## Orchestrator Rules
- Spawn subagents for focused tasks at each step
- Synthesize subagent results before proceeding
- Adapt workflow based on task complexity
- Skip/combine steps for simple requests