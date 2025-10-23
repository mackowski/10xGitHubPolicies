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
| `Status`      | `NVARCHAR(20)`| `NOT NULL`                                | Current status (e.g., 'InProgress', 'Completed'). |
| `StartedAt`   | `DATETIME2`   | `NOT NULL`                                | Timestamp when the scan was initiated.          |
| `CompletedAt` | `DATETIME2`   | `NULL`                                    | Timestamp when the scan finished.               |

---

#### **Table: `Repositories`**
*   **Purpose:** Stores a record of all scanned repositories and their current compliance status.

| Column               | Data Type      | Constraints                               | Description                                        |
|----------------------|----------------|-------------------------------------------|----------------------------------------------------|
| `RepositoryId`       | `INT`          | `PRIMARY KEY`, `IDENTITY(1,1)`            | Unique identifier for the repository record.       |
| `GitHubRepositoryId` | `BIGINT`       | `NOT NULL`, `UNIQUE`                      | The unique numeric ID from the GitHub API.         |
| `Name`               | `NVARCHAR(255)`| `NOT NULL`                                | The name of the repository.                        |
| `ComplianceStatus`   | `NVARCHAR(20)` | `NOT NULL`                                | Current status (e.g., 'Compliant', 'NonCompliant').|
| `LastScannedAt`      | `DATETIME2`    | `NULL`                                    | Timestamp of the last successful scan.             |

---

#### **Table: `Policies`**
*   **Purpose:** Stores a local copy of the policies defined in `config.yaml` for data integrity.

| Column      | Data Type       | Constraints                               | Description                                          |
|-------------|-----------------|-------------------------------------------|------------------------------------------------------|
| `PolicyId`  | `INT`           | `PRIMARY KEY`, `IDENTITY(1,1)`            | Unique identifier for the policy.                    |
| `PolicyKey` | `NVARCHAR(100)` | `NOT NULL`, `UNIQUE`                      | The unique key for the policy (e.g., 'has_agents_md'). |
| `Description` | `NVARCHAR(500)` | `NOT NULL`                                | A human-readable description of the policy.          |
| `Action`    | `NVARCHAR(50)`  | `NOT NULL`                                | Action to take on violation (e.g., 'create-issue').  |

---

#### **Table: `PolicyViolations`**
*   **Purpose:** Stores the specific policy violations found during a particular scan.

| Column         | Data Type | Constraints                               | Description                               |
|----------------|-----------|-------------------------------------------|-------------------------------------------|
| `ViolationId`  | `INT`     | `PRIMARY KEY`, `IDENTITY(1,1)`            | Unique identifier for the violation.      |
| `ScanId`       | `INT`     | `NOT NULL`, `FOREIGN KEY REFERENCES Scans(ScanId)` | Links to the scan that found the violation. |
| `RepositoryId` | `INT`     | `NOT NULL`, `FOREIGN KEY REFERENCES Repositories(RepositoryId)` | Links to the non-compliant repository.      |
| `PolicyId`     | `INT`     | `NOT NULL`, `FOREIGN KEY REFERENCES Policies(PolicyId)` | Links to the violated policy.             |

*   **Constraint:** A `UNIQUE` constraint will be applied to `(ScanId, RepositoryId, PolicyId)` to prevent duplicate entries per scan.

---

#### **Table: `ActionsLog`**
*   **Purpose:** Provides a persistent audit trail of automated actions taken by the system.

| Column         | Data Type       | Constraints                               | Description                                               |
|----------------|-----------------|-------------------------------------------|-----------------------------------------------------------|
| `ActionLogId`  | `INT`           | `PRIMARY KEY`, `IDENTITY(1,1)`            | Unique identifier for the log entry.                      |
| `RepositoryId` | `INT`           | `NOT NULL`, `FOREIGN KEY REFERENCES Repositories(RepositoryId)` | Links to the repository where the action was taken.         |
| `PolicyId`     | `INT`           | `NOT NULL`, `FOREIGN KEY REFERENCES Policies(PolicyId)` | Links to the policy that triggered the action.            |
| `ActionType`   | `NVARCHAR(50)`  | `NOT NULL`                                | The type of action taken (e.g., 'CreateIssue').           |
| `Timestamp`    | `DATETIME2`     | `NOT NULL`                                | Timestamp when the action was executed.                   |
| `Status`       | `NVARCHAR(20)`  | `NOT NULL`                                | The outcome of the action (e.g., 'Success', 'Failure').   |
| `Details`      | `NVARCHAR(MAX)` | `NULL`                                    | Additional details, like an issue URL or error message.   |

---

### 2. Relationships

*   **`PolicyViolations` to `Scans`:** Many-to-One (`PolicyViolations.ScanId` -> `Scans.ScanId`)
*   **`PolicyViolations` to `Repositories`:** Many-to-One (`PolicyViolations.RepositoryId` -> `Repositories.RepositoryId`)
*   **`PolicyViolations` to `Policies`:** Many-to-One (`PolicyViolations.PolicyId` -> `Policies.PolicyId`)
*   **`ActionsLog` to `Repositories`:** Many-to-One (`ActionsLog.RepositoryId` -> `Repositories.RepositoryId`)
*   **`ActionsLog` to `Policies`:** Many-to-One (`ActionsLog.PolicyId` -> `Policies.PolicyId`)

---

### 3. Indexes

*   **Clustered Indexes:** Automatically created on the `PRIMARY KEY` of each table.
*   **Non-Clustered Indexes:**
    *   `IX_Repositories_GitHubRepositoryId` on `Repositories(GitHubRepositoryId)`: To ensure fast lookups when processing data from the GitHub API.
    *   `IX_Repositories_Name` on `Repositories(Name)`: To support the dashboard's text-based filtering feature.
    *   `IX_Policies_PolicyKey` on `Policies(PolicyKey)`: To quickly find policies when synchronizing from `config.yaml`.
    *   `IX_PolicyViolations_RepositoryId` on `PolicyViolations(RepositoryId)`: To accelerate queries for retrieving all violations for a specific repository.

---


### 4. Design Notes

*   **Normalization:** The schema is normalized to reduce data redundancy. For example, policy details are stored once in the `Policies` table and referenced elsewhere.
*   **Data Integrity:** The use of foreign keys, unique constraints, and not-null constraints ensures a high level of data integrity.
*   **Auditability:** The `ActionsLog` table provides a clear and persistent audit trail for all automated actions, which is crucial for monitoring and debugging.
*   **Scalability:** The indexing strategy is designed to support efficient querying as the number of repositories and scans grows. Storing the `GitHubRepositoryId` allows for efficient synchronization and avoids relying on repository names, which can change.
