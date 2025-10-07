# 10x GitHub Policy Enforcer - Technology Stack

### Backend: ASP.NET Core

*   **Secure Authentication:** Provides a robust foundation for implementing the "Login with GitHub" OAuth flow (US-001) and managing user sessions securely.
*   **Centralized Logic:** Enables the creation of a centralized service to read the `config.yaml`, run scans, and enforce the core policies (e.g., file presence, workflow permissions) described in section 3.4.
*   **API Integration:** Serves as the backbone for all interactions with the GitHub API, handling the logic for scanning repositories and executing automated actions.

### Frontend: Blazor Server + MudBlazor

*   **Rapid UI Development:** The MudBlazor component library allows for the quick assembly of the compliance dashboard (US-006), including the repository list, filter, and summary metrics, accelerating MVP delivery.
*   **Simplified Real-time Updates:** Blazor Server's architecture provides real-time UI feedback for on-demand scans (US-008) out-of-the-box, removing the need for a separate, more complex real-time messaging service.
*   **Interactive Experience:** Facilitates the creation of a responsive user experience, including the real-time repository filter (US-007) and the guided setup for first-time users (US-003).

### Database: Azure SQL Database (Serverless Tier)

*   **Structured Data Storage:** Offers a reliable and structured way to store scan results, which is essential for displaying the list of non-compliant repositories and their specific violations on the dashboard (US-006).
*   **Efficient Metrics Calculation:** A relational model simplifies the queries needed to calculate and display the overall compliance percentage metric accurately on the dashboard.
*   **Cost-Effective Operation:** The serverless tier is highly cost-effective for an internal tool with periodic activity (scans) followed by idle periods, as it can scale compute resources down to zero.

### Background Jobs: Hangfire

*   **Scheduled Automation:** Directly fulfills the requirement for scheduled daily scans (US-009), ensuring that compliance is monitored continuously without manual intervention.
*   **On-Demand Execution:** Enables the "Scan Now" button functionality (US-008) by allowing background jobs to be triggered instantly, providing immediate feedback to users.
*   **Reliable Action Execution:** Provides a robust mechanism for reliably executing the automated actions defined in the configuration, such as creating GitHub issues (US-010) or archiving repositories (US-011).

### Hosting: Azure App Service

*   **Simplified Deployment:** As a fully managed Platform-as-a-Service (PaaS), it simplifies the deployment and hosting of the ASP.NET Core application, reducing operational overhead.
*   **Secure & Scalable Environment:** Provides a secure and scalable environment with excellent, native support for hosting .NET applications, ensuring the dashboard is reliably available to authorized team members.
*   **Seamless CI/CD Integration:** Integrates seamlessly with GitHub Actions, allowing for the creation of a fully automated CI/CD pipeline from code commit to deployment.

### GitHub Integration: Octokit.net

*   **Core Functional Enabler:** Provides the essential, strongly-typed .NET client for the GitHub API needed to perform all core functions, from checking for `AGENTS.md` (US-004) to verifying repository settings.
*   **Action Implementation:** Simplifies the implementation of the automated remediation actions, such as creating precisely formatted GitHub issues (US-010) and archiving repositories (US-011).
*   **Access Control:** Crucial for implementing security by checking a user's GitHub Team membership to authorize or deny access to the application dashboard (US-002, US-005).

### CI/CD: GitHub Actions

*   **Automated Delivery Pipeline:** Automates the entire build and deployment process, ensuring that new features and bug fixes are delivered to users quickly and reliably.
*   **Ensures Reliability:** Enables the creation of automated testing suites that verify the logic of policy checks and actions, directly supporting the "Action Reliability" success metric.
*   **Unified Ecosystem:** Keeps the entire development, testing, and deployment lifecycle within the GitHub ecosystem, providing a seamless workflow for developers.
