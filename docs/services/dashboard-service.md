# Dashboard Service

This document describes the `IDashboardService` and how it provides data for the Blazor frontend dashboard.

> **Note**: This document is part of the [Services Architecture](./architecture.md) documentation.

## Overview

The Dashboard Service acts as a "Backend for Frontend" (BFF) pattern, providing all necessary data for rendering the compliance dashboard. It queries the database, calculates metrics, and formats data specifically for the UI layer.

## Service Interface

### `IDashboardService`

- `Task<DashboardViewModel> GetDashboardViewModelAsync(string? nameFilter = null)`: Returns compliance metrics and non-compliant repositories, with optional name filtering

## Implementation

### `DashboardService`

**Dependencies**:
- `ApplicationDbContext`: For querying scan results and repository data

## View Model

### `DashboardViewModel`

Contains compliance metrics and non-compliant repositories:
- `CompliancePercentage`: Overall compliance (0-100)
- `TotalRepositories`: Total number of repositories
- `CompliantRepositories`: Number of compliant repositories
- `NonCompliantRepositories`: List of non-compliant repositories with violations

### `NonCompliantRepositoryViewModel`

Contains repository violation details:
- `Name`: Full repository name (e.g., "org/repo-name")
- `Url`: GitHub URL
- `ViolatedPolicies`: List of violated policy types

## Data Retrieval Logic

The service implements the following logic:

1. **Find Latest Completed Scan**: Queries for the most recent scan with status "Completed"
2. **Handle No Scans**: Returns empty view model if no scans have been completed
3. **Calculate Total Repositories**: Counts all repositories in the database
4. **Get Violations**: Retrieves all violations from the latest scan with related entities
5. **Calculate Compliance**: 
   - Identifies unique non-compliant repository IDs
   - Calculates compliant count: `Total - NonCompliant`
   - Calculates percentage: `(Compliant / Total) * 100`
6. **Filter Repositories**: Applies name filter if provided (case-insensitive partial match)
7. **Build View Model**: Constructs repository view models with violation details

## Filtering

The service supports filtering non-compliant repositories by name:

- **Case-Insensitive**: Filtering is case-insensitive
- **Partial Match**: Uses `Contains()` for partial matching
- **Empty Filter**: If `nameFilter` is null or empty, all non-compliant repositories are returned
- **Applied to Results**: Filter is applied after calculating compliance metrics

## Usage

```csharp
// Get dashboard data
var viewModel = await _dashboardService.GetDashboardViewModelAsync();

// With name filtering
var viewModel = await _dashboardService.GetDashboardViewModelAsync("search-term");
```

## Service Registration

Registered as a **scoped** service because it uses request-specific database context and view model data.

## Performance Considerations

- Uses LINQ with `Include()` for efficient eager-loading
- Minimal database round trips
- Name filtering applied at database level
- Only queries the most recent completed scan

## Edge Cases

### No Scans Completed
- Returns empty view model with default values
- `CompliancePercentage` defaults to 0
- `NonCompliantRepositories` is an empty list

### No Repositories
- `TotalRepositories` is 0
- `CompliancePercentage` is set to 100 (no violations if no repositories)
- `NonCompliantRepositories` is empty

### All Repositories Compliant
- `CompliantRepositories` equals `TotalRepositories`
- `CompliancePercentage` is 100
- `NonCompliantRepositories` is empty

## Related Documentation

- [Database Schema](../database.md) - Database structure and relationships
- [Scanning Service](./scanning-service.md) - How scans populate the data
- [Policy Evaluation Service](./policy-evaluation.md) - How violations are detected

