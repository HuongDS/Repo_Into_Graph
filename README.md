# Repo Into Graph API

This is the backend API for the **Repo Into Graph** project. The project has been restructured into a 3-layer architecture for better scalability, maintainability, and separation of concerns.

##  Architecture

The solution consists of three main layers:

1. **Repo_Into_Graph_API** (Presentation/API Layer)
   - Controllers (`AnalysisController`, `BusinessFlowsController`, `FewShotController`, etc.)
   - DTOs and Request/Response models
   - Exceptions handling and Global Error configurations
   - Application startup & Dependency Injection (`Program.cs`)

2. **Repo_Into_Graph_Application** (Business Logic/Service Layer)
   - Core Services (`AnalysisService`, `BusinessFlowService`, `FewShotService`, etc.)
   - Domain logic and Mapping configurations
   - Interfaces for abstractions

3. **Repo_Into_Graph_DataAccess** (Data/Infrastructure Layer)
   - Entity Models (`AnalysisRun`, `CallGraphEdge`, `BusinessFlow`, etc.)
   - Entity Framework Core DbContext (`AnalysisDbContext`)
   - Repository implementations (`GenericRepository`, `AnalysisRunRepository`, etc.)
   - Database Migrations

##  Getting Started

### Prerequisites

- .NET 8.0 SDK
- PostgreSQL database
- Docker (optional, for running Postgres locally)

### Configuration

1. Make sure to have a PostgreSQL instance running.
2. The project uses a `.env` file or `appsettings.json` for configurations.
3. Configure your `POSTGRES_CONNECTION_STRING` or the DefaultConnection inside `appsettings.json`.

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=repo_into_graph;Username=postgres;Password=postgres"
  }
}
```

### Build and Run

Run the API layer:

```bash
cd Repo_Into_Graph
dotnet restore Repo_Into_Graph.sln
dotnet run --project Repo_Into_Graph_API.csproj
```

Once running, you can access the Swagger UI documentation typically at:
- `http://localhost:<port>/swagger`

##  Database Migrations

The project uses Entity Framework Core for data access. To apply database migrations:

```bash
dotnet ef database update --project ../Repo_Into_Graph_DataAccess --startup-project Repo_Into_Graph_API.csproj
```
