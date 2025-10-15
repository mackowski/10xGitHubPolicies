# 10x GitHub Policy Enforcer

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)](https://github.com)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A GitHub App to automate the enforcement of organizational policies and security best practices across all repositories within a GitHub organization.

---

## Table of Contents

- [About The Project](#about-the-project)
- [Tech Stack](#tech-stack)
- [Getting Started](#getting-started)
  - [Prerequisites](#prerequisites)
  - [Installation](#installation)
- [Configuration](#configuration)
- [Project Scope](#project-scope)
- [Project Status](#project-status)
- [License](#license)

---

## About The Project

The 10x GitHub Policy Enforcer is a GitHub App with an accompanying web UI designed to automate the enforcement of organizational policies and security best practices across all repositories within a GitHub organization.

Key features include:
*   **Automated Scans:** Performs daily and on-demand scans of all active repositories.
*   **Policy Enforcement:** Verifies compliance against a central configuration file (`config.yaml`).
*   **Automated Actions:** Creates issues or archives repositories that violate policies.
*   **Compliance Dashboard:** Provides a web UI to view non-compliant repositories, violation details, and overall compliance metrics.

---

## Tech Stack

| Category          | Technology                               |
| ----------------- | ---------------------------------------- |
| **Backend**       | ASP.NET Core (.NET 8)                    |
| **Frontend**      | Blazor Server with MudBlazor             |
| **Database**      | Azure SQL Database (Serverless Tier)     |
| **Background Jobs** | Hangfire                                 |
| **Hosting**       | Azure App Service                        |
| **GitHub API**    | Octokit.net                              |
| **CI/CD**         | GitHub Actions                           |

---

## Getting Started

To get a local copy up and running, follow these simple steps.

### Prerequisites

*   [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
*   [Docker Desktop](https://www.docker.com/products/docker-desktop/)

### Installation

1.  Clone the repo:
    ```sh
    git clone https://github.com/your_username/10xGitHubPolicies.git
    ```
2.  Navigate to the project root directory:
    ```sh
    cd 10xGitHubPolicies
    ```
3. Start the local SQL Server database:
    ```sh
    docker-compose up -d
    ```
4.  Navigate to the application directory:
    ```sh
    cd 10xGitHubPolicies/10xGitHubPolicies.App
    ```
5.  Install dependencies:
    ```sh
    dotnet restore
    ```
6. Apply database migrations:
    ```sh
    dotnet ef database update
    ```
7.  Run the application:
    ```sh
    dotnet run
    ```
Alternatively, you can run the project from the root directory:
```sh
dotnet run --project 10xGitHubPolicies.App/10xGitHubPolicies.App.csproj
```

---

## Configuration

The application is configured via `appsettings.json` and user secrets for sensitive data.

### GitHub App Settings
You need to configure the GitHub App settings. During development, it's recommended to use the .NET Secret Manager.

1.  Initialize user secrets for the project:
    ```sh
    dotnet user-secrets init --project 10xGitHubPolicies.App/10xGitHubPolicies.App.csproj
    ```
2.  Set the GitHub App secrets:
    ```json
    "GitHubApp": {
      "AppId": "YOUR_APP_ID",
      "PrivateKey": "YOUR_PRIVATE_KEY",
      "InstallationId": "YOUR_INSTALLATION_ID"
    }
    ```

### Policy Configuration
The policy configuration is managed via a `config.yaml` file located in the root of your organization's `.github` repository.

Here is an example configuration:

```yaml
# .github/config.yaml

# Access control: Specify the GitHub team authorized to access the dashboard.
# Format: 'organization-slug/team-slug'
authorized_team: 'my-org/security-team'

# Policies: Define the rules to enforce across your organization's repositories.
policies:
  - name: 'Check for AGENTS.md'
    type: 'has_agents_md'
    action: 'create-issue' # 'create-issue' or 'archive-repo'
    issue_details:
      title: 'Compliance: AGENTS.md file is missing'
      body: 'This repository is missing the AGENTS.md file in its root directory. Please add this file to comply with organization standards.'
      labels: ['policy-violation', 'documentation']

  - name: 'Check for catalog-info.yaml'
    type: 'has_catalog_info_yaml'
    action: 'create-issue'
    issue_details:
      title: 'Compliance: catalog-info.yaml is missing'
      body: 'This repository is missing the catalog-info.yaml file. This file is required for backstage.io service discovery.'
      labels: ['policy-violation', 'backstage']
      
  - name: 'Verify Workflow Permissions'
    type: 'correct_workflow_permissions'
    action: 'archive-repo'
```

---

## Project Scope

### In Scope (MVP)
*   ⏳ `[todo]` Authentication via "Login with GitHub" OAuth flow.
*   ⏳ `[todo]` Access restricted to a specified GitHub Team.
*   ⏳ `[todo]` Configuration managed via a single `config.yaml` file in the `.github` repository.
*   ⏳ `[todo]` Daily and on-demand scanning of all active repositories.
*   ⏳ `[todo]` Core policies:
    *   Verify presence of `AGENTS.md`.
    *   Verify presence of `catalog-info.yaml`.
    *   Verify repository Workflow Permissions are set to 'Read repository contents and packages permissions'.
*   ⏳ `[todo]` Automated actions:
    *   Create a GitHub Issue in the non-compliant repository.
    *   Archive the non-compliant repository.
*   ⏳ `[todo]` A simple dashboard showing non-compliant repositories, violation details, overall compliance percentage, and a repository name filter.

### Out of Scope (MVP)
*   Automatically fixing policy violations (e.g., creating the missing file).
*   Support for other version control systems (e.g., GitLab, Bitbucket).
*   Advanced policy types (e.g., checking file content, branch protection rules).
*   User-level permissions within the application.
*   Repository-level exceptions or overrides in the UI.

---

## Project Status

This project is currently **in development**. The immediate focus is on delivering the Minimum Viable Product (MVP) features outlined in the project scope.

---

## License

Distributed under the MIT License. See `LICENSE` for more information.
