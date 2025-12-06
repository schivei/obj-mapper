# Contributing to omap

Thank you for your interest in contributing to omap! This document provides guidelines and instructions for contributing.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [How to Contribute](#how-to-contribute)
- [Pull Request Process](#pull-request-process)
- [Coding Standards](#coding-standards)
- [Testing](#testing)
- [Documentation](#documentation)

## Code of Conduct

This project adheres to a code of conduct. By participating, you are expected to uphold this code:

- Be respectful and inclusive
- Be patient with newcomers
- Focus on constructive feedback
- Accept responsibility for mistakes

## Getting Started

1. **Fork the repository** on GitHub
2. **Clone your fork** locally:
   ```bash
   git clone https://github.com/YOUR_USERNAME/obj-mapper.git
   cd obj-mapper
   ```
3. **Add upstream remote**:
   ```bash
   git remote add upstream https://github.com/schivei/obj-mapper.git
   ```

## Development Setup

### Prerequisites

- .NET 10.0 SDK or later
- A code editor (VS Code, Visual Studio, Rider)
- Git

### Building the Project

```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run tests
dotnet test
```

### Running the Tool Locally

```bash
cd src/ObjMapper
dotnet run -- --help
```

## How to Contribute

### Reporting Bugs

Before reporting a bug:
1. Check existing issues to avoid duplicates
2. Use the bug report template
3. Include:
   - Clear description of the issue
   - Steps to reproduce
   - Expected vs actual behavior
   - Environment details (OS, .NET version)
   - Relevant logs or error messages

### Suggesting Features

1. Check existing issues and discussions
2. Use the feature request template
3. Describe:
   - The problem you're trying to solve
   - Your proposed solution
   - Alternative solutions considered
   - Additional context

### Contributing Code

1. Find an issue to work on (or create one)
2. Comment on the issue to express interest
3. Fork and create a feature branch
4. Make your changes
5. Submit a pull request

## Pull Request Process

### Before Submitting

1. **Sync with upstream**:
   ```bash
   git fetch upstream
   git rebase upstream/main
   ```

2. **Create a feature branch**:
   ```bash
   git checkout -b feature/your-feature-name
   ```

3. **Make your changes** following our coding standards

4. **Run tests**:
   ```bash
   dotnet test
   ```

5. **Build successfully**:
   ```bash
   dotnet build
   ```

### Submitting the PR

1. Push your branch to your fork
2. Open a PR against `main` branch
3. Fill out the PR template completely
4. Link related issues using keywords (e.g., "Fixes #123")

### PR Requirements

- [ ] All tests pass
- [ ] New features have tests
- [ ] Documentation is updated
- [ ] Code follows project style
- [ ] Commit messages are clear
- [ ] No merge conflicts

### Review Process

1. Maintainers will review your PR
2. Address any feedback
3. Once approved, a maintainer will merge

## Coding Standards

### C# Style Guide

- Use meaningful variable and method names
- Follow Microsoft C# naming conventions
- Use `var` for obvious types
- Prefer expression-bodied members for simple methods
- Use file-scoped namespaces

### Code Organization

```
src/
├── ObjMapper/
│   ├── Generators/     # Code generators (EF Core, Dapper)
│   ├── Models/         # Data models
│   ├── Parsers/        # CSV parsers
│   ├── Services/       # Business logic
│   └── Program.cs      # Entry point
tests/
├── ObjMapper.Tests/           # Unit tests
└── ObjMapper.IntegrationTests/ # Integration tests
```

### Commit Messages

Use conventional commit format:

```
type(scope): description

[optional body]

[optional footer]
```

Types:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation
- `style`: Formatting
- `refactor`: Code restructuring
- `test`: Adding tests
- `chore`: Maintenance

Examples:
```
feat(generators): add stored procedure support
fix(type-mapper): handle nullable GUID columns
docs(readme): add legacy inference documentation
```

## Testing

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/ObjMapper.Tests

# Run with verbosity
dotnet test --verbosity normal

# Run specific test class
dotnet test --filter "FullyQualifiedName~StoredProcedureTests"
```

### Writing Tests

- Place tests in the appropriate test project
- Name test methods descriptively: `MethodName_Scenario_ExpectedResult`
- Use `[Theory]` for parameterized tests
- Mock external dependencies
- Test edge cases

Example:
```csharp
[Fact]
public void MapToCSharpType_Char36_ReturnsGuid()
{
    var mapper = new TypeMapper(DatabaseType.SqlServer);
    var result = mapper.MapToCSharpType("char(36)", false);
    Assert.Equal("Guid", result);
}
```

## Documentation

### Updating Documentation

- Update README.md for user-facing changes
- Add XML comments for public APIs
- Update CHANGELOG.md for releases

### Building Documentation

Documentation is in Markdown format and rendered by GitHub.

## Questions?

- Open a GitHub Discussion for questions
- Tag maintainers for urgent issues
- Join our community channels

Thank you for contributing! 🎉
