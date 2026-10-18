namespace PharmaAI.Application.Sync;

/// <summary>Wakes the background sender now instead of at its next scheduled check.</summary>
public interface ISyncTrigger
{
    void RequestSync();
}
