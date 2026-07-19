namespace TeamsAITeammate.Core.Interfaces;

public interface IKnowledgeStoreFactory
{
    IKnowledgeStore CreateStore(string providerName);
    IReadOnlyList<string> GetAvailableProviders();
}
