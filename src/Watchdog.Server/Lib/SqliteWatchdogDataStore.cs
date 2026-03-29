using Watchdog.Server.Models;

namespace Watchdog.Server.Lib;

public sealed class SqliteWatchdogDataStore : IWatchdogDataStore
{
    private readonly SqliteDatabaseRuntime _runtime;
    private readonly SqliteProjectRepository _projects;
    private readonly SqliteActivityRepository _activity;
    private readonly SqliteWorkflowRepository _workflow;
    private readonly SqliteInsightRepository _insights;

    public SqliteWatchdogDataStore()
    {
        _runtime = new SqliteDatabaseRuntime();
        _projects = new SqliteProjectRepository(_runtime);
        _activity = new SqliteActivityRepository(_runtime);
        _workflow = new SqliteWorkflowRepository(_runtime);
        _insights = new SqliteInsightRepository(_runtime);
    }

    public ProjectsConfig LoadProjects()
    {
        EnsureBootstrapped();
        return _projects.LoadProjects();
    }

    public Project? GetProject(string name)
    {
        EnsureBootstrapped();
        return _projects.GetProject(name);
    }

    public (Project Project, bool Existed) AddProject(string name, string path, ProjectWorkflowPolicy policy)
    {
        EnsureBootstrapped();
        return _projects.AddProject(name, path, policy);
    }

    public bool MarkProjectHooksInstalled(string name)
    {
        EnsureBootstrapped();
        return _projects.MarkProjectHooksInstalled(name);
    }

    public Project? UpdateProjectPolicy(string name, ProjectWorkflowPolicy policy)
    {
        EnsureBootstrapped();
        return _projects.UpdateProjectPolicy(name, policy);
    }

    public bool RemoveProject(string name)
    {
        EnsureBootstrapped();
        return _projects.RemoveProject(name);
    }

    public void AppendStreamEvent(StreamEvent streamEvent)
    {
        EnsureBootstrapped();
        _activity.AppendStreamEvent(streamEvent);
    }

    public StreamSlice ReadStreamSince(string project, int cursor, int limit)
    {
        EnsureBootstrapped();
        return _activity.ReadStreamSince(project, cursor, limit);
    }

    public int GetStreamCount(string project)
    {
        EnsureBootstrapped();
        return _activity.GetStreamCount(project);
    }

    public StreamEvent? GetLastStreamEvent(string project)
    {
        EnsureBootstrapped();
        return _activity.GetLastStreamEvent(project);
    }

    public void AppendAlert(SafetyAlert alert)
    {
        EnsureBootstrapped();
        _activity.AppendAlert(alert);
    }

    public List<SafetyAlert> ReadRecentAlerts(string project, int maxDays = 7)
    {
        EnsureBootstrapped();
        return _activity.ReadRecentAlerts(project, maxDays);
    }

    public List<SafetyAlert> ReadUnacknowledgedAlerts(string project, int maxDays = 7)
    {
        EnsureBootstrapped();
        return _activity.ReadUnacknowledgedAlerts(project, maxDays);
    }

    public bool AcknowledgeAlert(string project, string alertId)
    {
        EnsureBootstrapped();
        return _activity.AcknowledgeAlert(project, alertId);
    }

    public MailboxMessage EnqueueMailboxMessage(string project, MessagePriority priority, MessageType type,
        string content, NudgeTone? tone, bool requiresAck, TimeSpan expiresIn)
    {
        EnsureBootstrapped();
        return _activity.EnqueueMailboxMessage(project, priority, type, content, tone, requiresAck, expiresIn);
    }

    public MailboxMessage? ClaimNextMailboxMessage(string project)
    {
        EnsureBootstrapped();
        return _activity.ClaimNextMailboxMessage(project);
    }

    public bool AckMailboxMessage(string project, string msgId)
    {
        EnsureBootstrapped();
        return _activity.AckMailboxMessage(project, msgId);
    }

    public MailboxCounts GetMailboxCounts(string project)
    {
        EnsureBootstrapped();
        return _activity.GetMailboxCounts(project);
    }

    public void EnsureMailbox(string project)
    {
        EnsureBootstrapped();
        _ = _projects.GetProject(project);
    }

    public void AppendJob(string project, SubagentJob job)
    {
        EnsureBootstrapped();
        _workflow.AppendJob(project, job);
    }

    public bool ReplaceJob(string project, SubagentJob updatedJob)
    {
        EnsureBootstrapped();
        return _workflow.ReplaceJob(project, updatedJob);
    }

    public SubagentJob? GetJob(string project, string jobId)
    {
        EnsureBootstrapped();
        return _workflow.GetJob(project, jobId);
    }

    public List<SubagentJob> ListRecentJobs(string project, int max = 50)
    {
        EnsureBootstrapped();
        return _workflow.ListRecentJobs(project, max);
    }

    public List<SubagentJob> ListAllJobs(string project)
    {
        EnsureBootstrapped();
        return _workflow.ListAllJobs(project);
    }

    public void AppendEpisode(string project, Episode episode)
    {
        EnsureBootstrapped();
        _workflow.AppendEpisode(project, episode);
    }

    public List<Episode> ReadRecentEpisodes(string project, int maxDays = 7)
    {
        EnsureBootstrapped();
        return _workflow.ReadRecentEpisodes(project, maxDays);
    }

    public bool PatchEpisodeOutcome(string project, string episodeId, EpisodeOutcome outcome, DateTimeOffset reflectedAt)
    {
        EnsureBootstrapped();
        return _workflow.PatchEpisodeOutcome(project, episodeId, outcome, reflectedAt);
    }

    public void EnqueueReflection(PendingReflection reflection)
    {
        EnsureBootstrapped();
        _workflow.EnqueueReflection(reflection);
    }

    public List<PendingReflection> GetDueReflections(DateTimeOffset? asOf = null)
    {
        EnsureBootstrapped();
        return _workflow.GetDueReflections(asOf);
    }

    public void RemoveReflection(string episodeId)
    {
        EnsureBootstrapped();
        _workflow.RemoveReflection(episodeId);
    }

    public int GetReflectionCount()
    {
        EnsureBootstrapped();
        return _workflow.GetReflectionCount();
    }

    public List<PendingReflection> LoadAllReflections()
    {
        EnsureBootstrapped();
        return _workflow.LoadAllReflections();
    }

    public StrategyProfile LoadProfile()
    {
        EnsureBootstrapped();
        return _insights.LoadProfile();
    }

    public void SaveProfile(StrategyProfile profile)
    {
        EnsureBootstrapped();
        _insights.SaveProfile(profile);
    }

    public List<CrystallizedPattern> LoadPatterns()
    {
        EnsureBootstrapped();
        return _insights.LoadPatterns();
    }

    public List<CrystallizedPattern> ReadPatternsForProject(string project)
    {
        EnsureBootstrapped();
        return _insights.ReadPatternsForProject(project);
    }

    public void AppendPattern(CrystallizedPattern pattern)
    {
        EnsureBootstrapped();
        _insights.AppendPattern(pattern);
    }

    public bool TryAcquireLease(string name, int pid, TimeSpan ttl)
    {
        EnsureBootstrapped();
        return _insights.TryAcquireLease(name, pid, ttl);
    }

    public void ReleaseLease(string name, int pid)
    {
        EnsureBootstrapped();
        _insights.ReleaseLease(name, pid);
    }

    public LockInfo? ReadLease(string name)
    {
        EnsureBootstrapped();
        return _insights.ReadLease(name);
    }

    private void EnsureBootstrapped() => _runtime.EnsureBootstrapped();
}