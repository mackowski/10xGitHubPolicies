using _10xGitHubPolicies.App.Models.Configuration;

namespace _10xGitHubPolicies.App.Services.Implementations;

public class PolicyEvaluationService : IPolicyEvaluationService
{
    private readonly IEnumerable<IPolicyEvaluator> _evaluators;

    public PolicyEvaluationService(IEnumerable<IPolicyEvaluator> evaluators)
    {
        _evaluators = evaluators;
    }

    public async Task<IEnumerable<Data.Models.PolicyViolation>> EvaluateRepositoryAsync(Octokit.Repository repository, IEnumerable<PolicyConfig> policies)
    {
        var violations = new List<Data.Models.PolicyViolation>();

        foreach (var policy in policies)
        {
            var evaluator = _evaluators.FirstOrDefault(e => e.PolicyType.Equals(policy.Type, StringComparison.OrdinalIgnoreCase));
            if (evaluator != null)
            {
                var violation = await evaluator.EvaluateAsync(repository);
                if (violation != null)
                {
                    violations.Add(violation);
                }
            }
        }

        return violations;
    }
}
