using Watchdog.Server.Models;

namespace Watchdog.Server.Lib;

public interface IWatchdogDataStore
{
    ProjectsConfig LoadProjects();
    Project? GetProject(string name);
    (Project Project, bool Existed) AddProject(string name, string path, ProjectWorkflowPolicy policy);
    bool MarkProjectHooksInstalled(string name);
    Project? UpdateProjectPolicy(string name, ProjectWorkflowPolicy policy);
    bool RemoveProject(string name);

    void AppendStreamEvent(StreamEvent streamEvent);
    StreamSlice ReadStreamSince(string project, int cursor, int limit);
    int GetStreamCount(string project);
    StreamEvent? GetLastStreamEvent(string project);

    void AppendAlert(SafetyAlert alert);
    List<SafetyAlert> ReadRecentAlerts(string project, int maxDays = 7);
    List<SafetyAlert> ReadUnacknowledgedAlerts(string project, int maxDays = 7);
    bool AcknowledgeAlert(string project, string alertId);

    MailboxMessage EnqueueMailboxMessage(string project, MessagePriority priority, MessageType type,
        string content, NudgeTone? tone, bool requiresAck, TimeSpan expiresIn);
    MailboxMessage? ClaimNextMailboxMessage(string project);
    bool AckMailboxMessage(string project, string msgId);
    MailboxCounts GetMailboxCounts(string project);
    void EnsureMailbox(string project);

    void AppendJob(string project, SubagentJob job);
    bool ReplaceJob(string project, SubagentJob updatedJob);
    SubagentJob? GetJob(string project, string jobId);
    List<SubagentJob> ListRecentJobs(string project, int max = 50);
    List<SubagentJob> ListAllJobs(string project);

    void AppendEpisode(string project, Episode episode);
    List<Episode> ReadRecentEpisodes(string project, int maxDays = 7);
    bool PatchEpisodeOutcome(string project, string episodeId, EpisodeOutcome outcome, DateTimeOffset reflectedAt);

    void EnqueueReflection(PendingReflection reflection);
    List<PendingReflection> GetDueReflections(DateTimeOffset? asOf = null);
    void RemoveReflection(string episodeId);
    int GetReflectionCount();
    List<PendingReflection> LoadAllReflections();

    StrategyProfile LoadProfile();
    void SaveProfile(StrategyProfile profile);

    List<CrystallizedPattern> LoadPatterns();
    List<CrystallizedPattern> ReadPatternsForProject(string project);
    void AppendPattern(CrystallizedPattern pattern);

    bool TryAcquireLease(string name, int pid, TimeSpan ttl);
    void ReleaseLease(string name, int pid);
    LockInfo? ReadLease(string name);
}