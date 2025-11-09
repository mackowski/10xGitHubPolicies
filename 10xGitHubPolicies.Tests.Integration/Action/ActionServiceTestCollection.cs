using _10xGitHubPolicies.Tests.Integration.Fixtures;

namespace _10xGitHubPolicies.Tests.Integration.Action;

/// <summary>
/// Collection definition for ActionService integration tests.
/// This ensures all ActionService tests share the same database fixture,
/// reducing container startup time and resource usage.
/// </summary>
[CollectionDefinition("ActionService Integration Tests")]
public class ActionServiceTestCollection : ICollectionFixture<DatabaseFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}

