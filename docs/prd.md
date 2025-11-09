# Product Requirements Document (PRD) - 10x GitHub Policy Enforcer

## 1. Product Overview
The 10x GitHub Policy Enforcer is a GitHub App with an accompanying web UI designed to automate the enforcement of organizational policies and security best practices across all repositories within a GitHub organization. The application scans repositories for compliance against a centrally managed configuration file, performs automated actions on non-compliant repositories, and provides a simple dashboard for monitoring the overall compliance status.

## 2. User Problem
Engineering organizations, particularly Product Security Teams, face significant challenges in maintaining consistent standards and security postures across a growing number of GitHub repositories. The manual process of monitoring for compliance with organizational policies (e.g., presence of specific files, correct repository settings) is inefficient, prone to human error, and does not scale. This leads to security vulnerabilities, inconsistent project scaffolding, and a significant time investment for auditing and remediation. An automated tool is needed to continuously monitor compliance and enforce policies systematically.

## 3. Functional Requirements

### 3.1. Authentication and Authorization
*   Users must authenticate to the web UI via a "Login with GitHub" OAuth flow.
*   Access to the application's dashboard and features is restricted to members of a specific GitHub Team.
*   The authorized GitHub Team is defined in the application's configuration file.

### 3.2. Configuration
*   All application settings, including policies, actions, and access control, are managed through a single YAML file named `config.yaml`.
*   This configuration file must be located in the organization's `.github` repository.

### 3.3. Repository Scanning
*   The application scans all active (non-archived) repositories within the organization.
*   Scans are executed automatically on a daily schedule.
*   Users can trigger an "on-demand" scan manually from the web UI for immediate feedback.

### 3.4. Core Policies (MVP)
The application will support the following policies in its initial version:
1.  Verify the presence of an `AGENTS.md` file in the repository's root.
2.  Verify the presence of a `catalog-info.yaml` file in the repository's root.
3.  Verify that the `catalog-info.yaml` file contains an assigned owner in the `spec.owner` field.
4.  Verify that the repository's Workflow Permissions are set to 'Read repository contents and packages permissions'.

### 3.5. Automated Actions (MVP)
Based on policy violations, the application can be configured to take the following actions:
1.  *Create GitHub Issue*: Creates a labeled issue in the non-compliant repository. The application will prevent the creation of duplicate issues for the same violation.
2.  *Archive Repository*: Automatically archives the non-compliant repository, making it read-only.

### 3.6. Dashboard
The web UI will feature a simple dashboard that includes:
*   A list of all non-compliant repositories.
*   Details of the specific policies each repository is violating.
*   A summary metric showing the organization's overall repository compliance percentage.
*   A button to trigger an on-demand, organization-wide scan.
*   A simple text-based filter to search for repositories by name.

## 4. Product Boundaries

### 4.1. In Scope for MVP
*   All functional requirements listed in section 3.
*   Support for a single GitHub organization per installation.
*   A hosted web UI for the dashboard.

### 4.2. Out of Scope for MVP
*   Automatically fixing policy violations (e.g., creating the missing file or changing the setting).
*   Support for other version control systems like GitLab or Bitbucket.
*   Advanced policy types (e.g., checking file content, branch protection rules).
*   User-level permissions within the application; all authorized users have the same level of access.
*   Support for repository-level exceptions or overrides in the UI.

## 5. User Stories

### ID: US-001
*   Title: User Login with GitHub
*   Description: As a user, I want to log in to the application using my GitHub account so that I can access the dashboard securely.
*   Acceptance Criteria:
    1.  The login page presents a "Login with GitHub" button.
    2.  Clicking the button redirects the user to the GitHub OAuth authorization screen.
    3.  After successful authorization, the user is redirected back to the application's dashboard.
    4.  If the user is a member of the authorized team, they are successfully logged in and can view the dashboard.

### ID: US-002
*   Title: Restricted Access for Unauthorized Users
*   Description: As a GitHub user who is not part of the authorized team, I should be prevented from accessing the application's dashboard to ensure security.
*   Acceptance Criteria:
    1.  After a user logs in with GitHub, the application checks their team membership.
    2.  If the user is not a member of the team specified in `config.yaml`, they are redirected to an "Access Denied" page.
    3.  The page clearly explains that the user does not have permission to access the application.

### ID: US-003
*   Title: First-Time User Onboarding
*   Description: As a first-time user, I want to be guided on how to set up the required configuration file so that I can get the application running quickly.
*   Acceptance Criteria:
    1.  Upon first login, if the `config.yaml` file is not found in the organization's `.github` repository, the UI displays an onboarding message.
    2.  The message provides clear instructions on how to create the `config.yaml` file at the correct location.
    3.  A template for the `config.yaml` file is provided in the UI for the user to copy.
    4.  The main dashboard functionality is hidden or disabled until the configuration file is present and valid.

### ID: US-004
*   Title: Configure Policies and Actions
*   Description: As an administrator, I want to define policies and their corresponding actions in a YAML file so that I can customize how the application enforces compliance.
*   Acceptance Criteria:
    1.  The application reads its configuration from a YAML file located at `.github/config.yaml`.
    2.  The user can define a list of policies to enforce from the available types (`has_agents_md`, `has_catalog_info_yaml`, `correct_workflow_permissions`).
    3.  For each policy, the user can specify the action to take upon violation (`create-issue` or `archive-repo`).
    4.  Changes to the configuration file are automatically loaded by the application before the next scan runs.

### ID: US-005
*   Title: Configure Access Control
*   Description: As an administrator, I want to specify which GitHub Team is authorized to access the application's UI in the configuration file to control access.
*   Acceptance Criteria:
    1.  The user can specify a GitHub Team slug (e.g., `my-org/security-team`) in the `config.yaml` file.
    2.  Only members of the specified team can access the application's dashboard after logging in.

### ID: US-006
*   Title: View Compliance Dashboard
*   Description: As a user, I want to view a dashboard that summarizes the compliance status of all repositories so I can get a quick overview of the organization's health.
*   Acceptance Criteria:
    1.  The dashboard displays a list of all non-compliant repositories found in the last scan.
    2.  For each repository in the list, the specific policies being violated are clearly shown.
    3.  The dashboard displays a summary metric of the overall compliance percentage, calculated as (compliant repos / total scanned repos) * 100.

### ID: US-007
*   Title: Filter Repositories on Dashboard
*   Description: As a user, I want to filter the list of non-compliant repositories by name so that I can easily find a specific repository I am interested in.
*   Acceptance Criteria:
    1.  The dashboard contains a text input field for filtering the repository list.
    2.  As the user types into the filter, the list of repositories updates in real-time to show only repositories whose names contain the filter text.

### ID: US-008
*   Title: Trigger On-Demand Scan
*   Description: As a user, I want to manually trigger a scan of all repositories so that I can get immediate feedback after making changes.
*   Acceptance Criteria:
    1.  The dashboard has a "Scan Now" button.
    2.  Clicking the button initiates a new scan of all active repositories in the organization.
    3.  The UI provides visual feedback that a scan is in progress.
    4.  Upon completion, the dashboard is automatically refreshed with the latest scan results.

### ID: US-009
*   Title: Scheduled Daily Scans
*   Description: As a user, I want the application to automatically scan all repositories daily so that compliance is monitored continuously without manual intervention.
*   Acceptance Criteria:
    1.  The system automatically triggers a full scan of all repositories once every 24 hours.
    2.  The results of the scheduled scan update the data presented on the compliance dashboard.

### ID: US-010
*   Title: Automated Issue Creation
*   Description: As the system, when a repository violates a policy configured with the 'create-issue' action, I must create a GitHub issue in that repository to notify the owners.
*   Acceptance Criteria:
    1.  When a violation is detected, a GitHub issue is created in the non-compliant repository.
    2.  The issue title and body clearly describe the policy violation and provide guidance for remediation.
    3.  The issue is assigned a specific, configurable label (e.g., 'policy-violation').
    4.  The system checks for an existing open issue with the same label and title to prevent creating duplicate issues for the same unresolved violation.

### ID: US-011
*   Title: Automated Repository Archiving
*   Description: As the system, when a repository violates a policy configured with the 'archive-repo' action, I must archive the repository to prevent its further use.
*   Acceptance Criteria:
    1.  When a violation is detected for a policy with the 'archive-repo' action, the system makes an API call to GitHub to archive the corresponding repository.
    2.  The repository is successfully archived and becomes read-only.
    3.  The action is logged by the application for audit purposes.

## 6. Success Metrics
The success of the 10x GitHub Policy Enforcer will be measured by the following key metrics:
*   *Overall Compliance Rate*: The percentage of repositories in the organization that are compliant with all configured policies. Success is defined as a steady increase in this metric over time.
*   *Violation Remediation Time*: The average time between the creation of a policy violation issue by the application and its closure. A decrease in this time indicates the tool is effectively driving action.
*   *Manual Effort Reduction*: Qualitative feedback from the Product Security Team indicating a reduction in time spent manually auditing repositories.
*   *Dashboard Accuracy*: The dashboard accurately reflects the compliance status of all repositories based on the latest scan data.
*   *Action Reliability*: All configured actions (issue creation, repository archiving) are executed successfully and reliably for every detected violation.
