namespace TeamsAITeammate.Core.Interfaces;

public interface IDataSyncService
{
    Task SyncToSecondaryAsync(string tenantId, CancellationToken ct);
    Task StartChangeFeedProcessorAsync(CancellationToken ct);
    Task StopChangeFeedProcessorAsync(CancellationToken ct);
}
