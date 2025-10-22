namespace _10xGitHubPolicies.App.Exceptions;

public class ConfigurationNotFoundException : Exception
{
    public ConfigurationNotFoundException()
    {
    }

    public ConfigurationNotFoundException(string message)
        : base(message)
    {
    }

    public ConfigurationNotFoundException(string message, Exception inner)
        : base(message, inner)
    {
    }
}