# DocManager API

A document management REST API built with ASP.NET Core 9, Entity Framework Core, and SQLite.

## Features

- **Documents**: CRUD with file upload/download, versioning (previous versions kept on update)
- **Folders**: Tree structure with parent-child relationships
- **Tags**: CRUD and document tagging (many-to-many)
- **Search**: Full-text search across document names and content metadata
- **Users**: Basic CRUD for user management

## Tech Stack

- .NET 9 / ASP.NET Core Minimal API with Controllers
- Entity Framework Core + SQLite
- xUnit + WebApplicationFactory for integration tests

## Running

```bash
dotnet build
dotnet run --project src/DocManager
```

## Testing

```bash
dotnet test
```

## Known Limitations

- Search endpoint returns all results without pagination
- Authorization checks are duplicated inline in Documents and Folders controllers
- Document versioning has no test coverage
