# .NET 10.0 Upgrade Report

## Project target framework modifications

| Project name                                                  | Old Target Framework | New Target Framework | Commits                   |
|:--------------------------------------------------------------|:--------------------:|:--------------------:|:--------------------------|
| src/ObjMapper/ObjMapper.csproj                                | net9.0               | net10.0              | 0415eebd, 1abfc82f        |
| tests/ObjMapper.Tests/ObjMapper.Tests.csproj                  | net9.0               | net10.0              | ee3472fe                  |
| tests/ObjMapper.IntegrationTests/ObjMapper.IntegrationTests.csproj | net9.0          | net10.0              | 2112ef13, 0e671cdd        |

## NuGet Packages

| Package Name          | Old Version | New Version | Commit Id                 |
|:----------------------|:-----------:|:-----------:|:--------------------------|
| Microsoft.Data.Sqlite | 9.0.0       | 10.0.0      | 1abfc82f, 0e671cdd        |

## All commits

| Commit ID | Description                                                      |
|:----------|:-----------------------------------------------------------------|
| c2220ffd  | Commit upgrade plan                                              |
| 0415eebd  | Update ObjMapper.csproj to target net10.0                        |
| 1abfc82f  | Update Microsoft.Data.Sqlite to v10.0.0 in ObjMapper.csproj      |
| ee3472fe  | Update target framework to net10.0 in ObjMapper.Tests.csproj     |
| 2112ef13  | Update target framework to net10.0 in ObjMapper.IntegrationTests.csproj |
| 0e671cdd  | Update Microsoft.Data.Sqlite to v10.0.0 in IntegrationTests.csproj |

## Test Results

| Project                        | Passed | Failed | Skipped |
|:-------------------------------|:------:|:------:|:-------:|
| ObjMapper.Tests                | 134    | 0      | 0       |
| ObjMapper.IntegrationTests     | 7      | 0      | 12      |

## Summary

The upgrade from .NET 9.0 to .NET 10.0 completed successfully for all 3 projects in the solution. All unit tests passed validation.
