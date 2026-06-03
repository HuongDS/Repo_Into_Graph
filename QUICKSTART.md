# Quick Start Guide

## ?? Installation

### Prerequisites
- .NET 8 SDK or later
- A C# project or source code directory

### Build from Source
```bash
git clone <repository-url>
cd Repo_Into_Graph
dotnet build -c Release
```

The executable will be located at:
```
bin\Release\net8.0\Repo_Into_Graph.exe
```

## ?? Quick Start

### Step 1: Navigate to Project Directory
```bash
cd Repo_Into_Graph
```

### Step 2: Build the Project
```bash
dotnet build
```

### Step 3: Run Analysis
```bash
# Using relative path from project root
dotnet run -- "Repo_Into_Graph\Samples" "output"

# Or using the compiled executable
.\bin\Debug\net8.0\Repo_Into_Graph.exe "Repo_Into_Graph\Samples" "output"
```

### Step 4: Check Results
The `output` directory will contain:
- ? `output_graph.json` - Complete analysis data
- ? `call_graph.md` - Mermaid diagram of method calls
- ? `data_flow_graph.md` - Mermaid diagram of data flow

## ?? Sample Output

After running the analyzer on the sample code, you'll get:

### Call Graph
Shows method invocation relationships:
```
UserService.GetUserByIdAsync() ? UserRepository.GetByIdAsync()
UserService.CreateUserAsync() ? UserService.ValidateUser()
UserService.CreateUserAsync() ? UserRepository.SaveAsync()
UserService.CreateUserAsync() ? UserService.NotifyUserCreated()
```

### Data Flow
Shows how the `user` object flows through the system:
```
Source: UserService.CreateUserAsync
  ?
Passed Through: ValidateUser()
  ?
Passed Through: SaveAsync()
  ?
Passed Through: NotifyUserCreated()
  ?
Sink: return statement
```

## ?? Project Structure

```
Repo_Into_Graph/
??? Models/
?   ??? CallGraphEdge.cs      # Call graph edge model
?   ??? DataFlowNode.cs       # Data flow node model
?   ??? AnalysisResult.cs     # Combined analysis result
??? Services/
?   ??? CallGraphExtractor.cs  # Extracts method calls
?   ??? DataFlowGraphExtractor.cs # Extracts data flows
?   ??? CodeAnalyzer.cs        # Main orchestrator
?   ??? MermaidGenerator.cs    # Generates Mermaid diagrams
?   ??? OutputWriter.cs        # Writes output files
??? Samples/
?   ??? User.cs                # Sample User model
?   ??? UserService.cs         # Sample service class
?   ??? UserRepository.cs      # Sample repository
??? Program.cs                 # CLI entry point
??? Repo_Into_Graph.csproj    # Project file
??? README.md                  # Full documentation
```

## ?? Typical Workflow

### 1. Analyze Your Project
```bash
Repo_Into_Graph "C:\YourProject\src" "C:\YourProject\analysis"
```

### 2. View JSON Results
Open `analysis\output_graph.json` in any JSON viewer to see:
- All method invocations
- All data flow paths
- File paths and line numbers

### 3. View Mermaid Diagrams
Open `analysis\call_graph.md` and `analysis\data_flow_graph.md` in:
- GitHub (renders Mermaid natively)
- Any Markdown viewer with Mermaid support
- Mermaid Live Editor (https://mermaid.live)

### 4. Process Results
Use the JSON output in your research pipeline:
```python
# Example: Load and process in Python
import json

with open('output_graph.json', 'r') as f:
    data = json.load(f)

# Analyze call graph
for edge in data['CallGraph']:
    print(f"{edge['CallerClass']}.{edge['CallerMethod']} -> {edge['CalleeClass']}.{edge['CalleeMethod']}")

# Analyze data flow
for node in data['DataFlowGraph']:
    print(f"Variable {node['VariableName']} of type {node['DataType']}")
    print(f"  Source: {node['SourceLocation']}")
    print(f"  Passed through: {', '.join(node['PassedThroughMethods'])}")
    print(f"  Sink: {node['SinkLocation']}")
```

## ?? Common Use Cases

### 1. Visualize API Call Chain
```bash
Repo_Into_Graph "C:\MyAPI\Controllers" "output"
```
View `call_graph.md` to see the controller method call hierarchy.

### 2. Track Data Transformation
```bash
Repo_Into_Graph "C:\MyProject" "output"
```
View `data_flow_graph.md` to understand how data models flow through your system.

### 3. Identify Unused Methods
```bash
# Analyze the JSON to find methods with zero callers
jq '.CallGraph[] | select(.CallerClass == "MyClass") | .CalleeMethod' output_graph.json
```

### 4. Generate Architecture Diagram
```bash
Repo_Into_Graph "C:\MyProject" "docs"
# Copy call_graph.md content to your documentation
```

## ?? Configuration

### Customize Namespace Filtering
Edit `Services/CallGraphExtractor.cs`:
```csharp
private readonly HashSet<string> _standardNamespaces = new()
{
    "System",
    "System.Collections",
    // Add more namespaces to filter
};
```

### Customize Primitive Type Filtering
Edit `Services/DataFlowGraphExtractor.cs`:
```csharp
private bool IsPrimitiveType(string typeName)
{
    var primitiveTypes = new[] { 
        "int", "string", "bool", 
        // Add more types
    };
    return primitiveTypes.Contains(typeName.ToLower());
}
```

## ?? Troubleshooting

### Error: "Repository path does not exist"
- Verify the path is correct and absolute
- Check that the path ends with the source code directory
- Example: ? `C:\MyProject\src` instead of ? `C:\MyProject`

### Error: "No files found"
- Ensure the directory contains `.cs` files
- Check that files are not in `bin` or `obj` directories (auto-filtered)
- Verify file permissions

### Empty Results
- Check that code contains business logic (not just empty classes)
- Verify method visibility (private/public)
- Ensure methods have invocations or variable flows

### Slow Analysis
- Exclude large dependencies (put in `bin` or `obj`)
- Run on a subset of files first
- Check system resource availability

## ?? Next Steps

1. **Read Full Documentation**: See `README.md` for detailed information
2. **Review Technical Specs**: See `TECHNICAL_SPECIFICATION.md` for implementation details
3. **Explore Sample Output**: Check the `output` directory for example results
4. **Integrate into Pipeline**: Use JSON output in your research workflow
5. **Extend Functionality**: Modify extractors for custom analysis

## ?? Support

For issues or questions:
1. Check the README.md and TECHNICAL_SPECIFICATION.md
2. Review the sample code in `Repo_Into_Graph/Samples/`
3. Check the generated output files for analysis details
4. Verify project structure matches expectations

---

**Happy Analyzing! ??**
