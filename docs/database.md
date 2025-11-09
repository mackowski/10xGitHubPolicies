# Azure SQL Database Schema for 10x GitHub Policy Enforcer

This document outlines the database schema for the 10x GitHub Policy Enforcer application, designed for Azure SQL Database. The schema is optimized to support the functional requirements outlined in the PRD, focusing on tracking repository compliance, scan history, and automated actions.

---

### 1. Tables

This section details the tables, columns, data types, and constraints for the database.

#### **Table: `Scans`**
*   **Purpose:** Tracks the execution history and status of each repository scan.

| Column        | Data Type     | Constraints                               | Description                                     |
|---------------|---------------|-------------------------------------------|-------------------------------------------------|
| `ScanId`      | `INT`         | `PRIMARY KEY`, `IDENTITY(1,1)`            | Unique identifier for the scan.                 |
| `Status`      | `NVARCHAR(MAX)`| `NOT NULL`                                | Current status (e.g., 'InProgress', 'Completed'). |
| `StartedAt`   | `DATETIME2`   | `NOT NULL`                                | Timestamp when the scan was initiated.          |
| `CompletedAt` | `DATETIME2`   | `NULL`                                    | Timestamp when the scan finished.               |

---

#### **Table: `Repositories`**
*   **Purpose:** Stores a record of all scanned repositories and their current compliance status.

| Column               | Data Type      | Constraints                               | Description                                        |
|----------------------|----------------|-------------------------------------------|----------------------------------------------------|
| `RepositoryId`       | `INT`          | `PRIMARY KEY`, `IDENTITY(1,1)`            | Unique identifier for the repository record.       |
| `GitHubRepositoryId` | `BIGINT`       | `NOT NULL`, `UNIQUE`                      | The unique numeric ID from the GitHub API.         |
| `Name`               | `NVARCHAR(450)`| `NOT NULL`                                | The name of the repository.                        |
| `ComplianceStatus`   | `NVARCHAR(MAX)` | `NOT NULL`                                | Current status (e.g., 'Compliant', 'NonCompliant').|
| `LastScannedAt`      | `DATETIME2`    | `NULL`                                    | Timestamp of the last successful scan.             |

---

#### **Table: `Policies`**
*   **Purpose:** Stores a local copy of the policies defined in `config.yaml` for data integrity.

| Column      | Data Type       | Constraints                               | Description                                          |
|-------------|-----------------|-------------------------------------------|------------------------------------------------------|
| `PolicyId`  | `INT`           | `PRIMARY KEY`, `IDENTITY(1,1)`            | Unique identifier for the policy.                    |
| `PolicyKey` | `NVARCHAR(450)` | `NOT NULL`, `UNIQUE`                      | The unique key for the policy (e.g., 'has_agents_md'). |
| `Description` | `NVARCHAR(MAX)` | `NOT NULL`                                | A human-readable description of the policy.          |
| `Action`    | `NVARCHAR(MAX)`  | `NOT NULL`                                | Action to take on violation (e.g., 'create-issue').  |

---

#### **Table: `PolicyViolations`**
*   **Purpose:** Stores the specific policy violations found during a particular scan.

| Column         | Data Type | Constraints                               | Description                               |
|----------------|-----------|-------------------------------------------|-------------------------------------------|
| `ViolationId`  | `INT`     | `PRIMARY KEY`, `IDENTITY(1,1)`            | Unique identifier for the violation.      |
| `ScanId`       | `INT`     | `NOT NULL`, `FOREIGN KEY REFERENCES Scans(ScanId) ON DELETE CASCADE` | Links to the scan that found the violation. |
| `RepositoryId` | `INT`     | `NOT NULL`, `FOREIGN KEY REFERENCES Repositories(RepositoryId) ON DELETE CASCADE` | Links to the non-compliant repository.      |
| `PolicyId`     | `INT`     | `NOT NULL`, `FOREIGN KEY REFERENCES Policies(PolicyId) ON DELETE CASCADE` | Links to the violated policy.             |

*   **Constraint:** A `UNIQUE` constraint is applied to `(ScanId, RepositoryId, PolicyId)` to prevent duplicate entries per scan.
*   **Note:** The `PolicyViolation` entity includes a `PolicyType` property that is marked with `[NotMapped]` and is not stored in the database. This property is used for application logic only.

---

#### **Table: `ActionsLogs`**
*   **Purpose:** Provides a persistent audit trail of automated actions taken by the system.

| Column         | Data Type       | Constraints                               | Description                                               |
|----------------|-----------------|-------------------------------------------|-----------------------------------------------------------|
| `ActionLogId`  | `INT`           | `PRIMARY KEY`, `IDENTITY(1,1)`            | Unique identifier for the log entry.                      |
| `RepositoryId` | `INT`           | `NOT NULL`, `FOREIGN KEY REFERENCES Repositories(RepositoryId) ON DELETE CASCADE` | Links to the repository where the action was taken.         |
| `PolicyId`     | `INT`           | `NOT NULL`, `FOREIGN KEY REFERENCES Policies(PolicyId) ON DELETE CASCADE` | Links to the policy that triggered the action.            |
| `ActionType`   | `NVARCHAR(MAX)` | `NOT NULL`                                | The type of action taken (e.g., 'CreateIssue').           |
| `Timestamp`    | `DATETIME2`     | `NOT NULL`                                | Timestamp when the action was executed.                   |
| `Status`       | `NVARCHAR(MAX)` | `NOT NULL`                                | The outcome of the action (e.g., 'Success', 'Failure').   |
| `Details`      | `NVARCHAR(MAX)` | `NULL`                                    | Additional details, like an issue URL or error message.   |

---

### 2. Relationships

*   **`PolicyViolations` to `Scans`:** Many-to-One (`PolicyViolations.ScanId` -> `Scans.ScanId`) with `ON DELETE CASCADE`
*   **`PolicyViolations` to `Repositories`:** Many-to-One (`PolicyViolations.RepositoryId` -> `Repositories.RepositoryId`) with `ON DELETE CASCADE`
*   **`PolicyViolations` to `Policies`:** Many-to-One (`PolicyViolations.PolicyId` -> `Policies.PolicyId`) with `ON DELETE CASCADE`
*   **`ActionsLogs` to `Repositories`:** Many-to-One (`ActionsLogs.RepositoryId` -> `Repositories.RepositoryId`) with `ON DELETE CASCADE`
*   **`ActionsLogs` to `Policies`:** Many-to-One (`ActionsLogs.PolicyId` -> `Policies.PolicyId`) with `ON DELETE CASCADE`

---

### 3. Indexes

*   **Clustered Indexes:** Automatically created on the `PRIMARY KEY` of each table.
*   **Non-Clustered Indexes:**
    *   `IX_Repositories_GitHubRepositoryId` on `Repositories(GitHubRepositoryId)` (UNIQUE): To ensure fast lookups when processing data from the GitHub API.
    *   `IX_Repositories_Name` on `Repositories(Name)`: To support the dashboard's text-based filtering feature.
    *   `IX_Policies_PolicyKey` on `Policies(PolicyKey)` (UNIQUE): To quickly find policies when synchronizing from `config.yaml`.
    *   `IX_PolicyViolations_RepositoryId` on `PolicyViolations(RepositoryId)`: To accelerate queries for retrieving all violations for a specific repository.
    *   `IX_PolicyViolations_PolicyId` on `PolicyViolations(PolicyId)`: To support queries filtering by policy.
    *   `IX_PolicyViolations_ScanId_RepositoryId_PolicyId` on `PolicyViolations(ScanId, RepositoryId, PolicyId)` (UNIQUE): To prevent duplicate violation entries per scan.
    *   `IX_ActionsLogs_RepositoryId` on `ActionsLogs(RepositoryId)`: To accelerate queries for retrieving all actions for a specific repository.
    *   `IX_ActionsLogs_PolicyId` on `ActionsLogs(PolicyId)`: To support queries filtering by policy.

---


### 4. Design Notes

*   **Normalization:** The schema is normalized to reduce data redundancy. For example, policy details are stored once in the `Policies` table and referenced elsewhere.
*   **Data Integrity:** The use of foreign keys, unique constraints, and not-null constraints ensures a high level of data integrity.
*   **Auditability:** The `ActionsLogs` table provides a clear and persistent audit trail for all automated actions, which is crucial for monitoring and debugging.
*   **Cascade Deletes:** All foreign key relationships use `ON DELETE CASCADE` to ensure referential integrity. When a parent record (Scan, Repository, or Policy) is deleted, all related child records are automatically removed.
*   **Entity Framework Core:** The schema is managed using Entity Framework Core migrations. The `ApplicationDbContext` defines the DbSets and index configurations using Fluent API in the `OnModelCreating` method.
*   **Hangfire Tables:** In addition to the application tables, Hangfire creates its own tables in the database for background job management. These tables are automatically created by Hangfire when the application starts and are not part of the Entity Framework Core schema.
*   **Scalability:** The indexing strategy is designed to support efficient querying as the number of repositories and scans grows. Storing the `GitHubRepositoryId` allows for efficient synchronization and avoids relying on repository names, which can change.
*   **Production Deployment:** Database migrations are run automatically during CI/CD deployment using the `Tools/DbMigrator` console application. The production database uses Azure SQL Database with Managed Identity authentication for secretless access.
