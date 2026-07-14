using System.Collections.Concurrent;
using EduGuardProject.Services.IServices;

namespace EduGuardProject.Services;

public sealed class ExamPresenceTracker : IExamPresenceTracker
{
    private readonly ConcurrentDictionary<string, ExamPresenceConnection> _connections = new();
    private readonly ConcurrentDictionary<Guid, PresenceEntry> _presence = new();

    public bool Connect(
        string connectionId,
        Guid participationId,
        Guid examSlotId,
        Guid studentId,
        string fullName,
        DateTime connectedAt)
    {
        if (_connections.ContainsKey(connectionId))
            Disconnect(connectionId);

        var connection = new ExamPresenceConnection(
            participationId,
            examSlotId,
            studentId,
            fullName,
            connectedAt);

        _connections[connectionId] = connection;
        var entry = _presence.GetOrAdd(participationId, _ => new PresenceEntry());

        lock (entry.SyncRoot)
        {
            var wasOffline = entry.ConnectionIds.Count == 0;
            entry.ConnectionIds.Add(connectionId);
            entry.LastSeenAt = connectedAt;
            return wasOffline;
        }
    }

    public void Heartbeat(Guid participationId, DateTime seenAt)
    {
        var entry = _presence.GetOrAdd(participationId, _ => new PresenceEntry());
        lock (entry.SyncRoot)
        {
            if (seenAt > entry.LastSeenAt)
                entry.LastSeenAt = seenAt;
        }
    }

    public ExamPresenceDisconnect? Disconnect(string connectionId)
    {
        if (!_connections.TryRemove(connectionId, out var connection))
            return null;

        if (!_presence.TryGetValue(connection.ParticipationId, out var entry))
            return new ExamPresenceDisconnect(connection, true);

        lock (entry.SyncRoot)
        {
            entry.ConnectionIds.Remove(connectionId);
            var becameOffline = entry.ConnectionIds.Count == 0;
            if (becameOffline)
                entry.LastSeenAt = default;

            return new ExamPresenceDisconnect(connection, becameOffline);
        }
    }

    public void MarkOffline(Guid participationId)
    {
        if (_presence.TryRemove(participationId, out var entry))
        {
            lock (entry.SyncRoot)
            {
                foreach (var connectionId in entry.ConnectionIds)
                    _connections.TryRemove(connectionId, out _);

                entry.ConnectionIds.Clear();
            }
        }
    }

    public DateTime? GetLastSeen(Guid participationId)
    {
        if (!_presence.TryGetValue(participationId, out var entry))
            return null;

        lock (entry.SyncRoot)
            return entry.LastSeenAt == default ? null : entry.LastSeenAt;
    }

    public bool IsOnline(Guid participationId, DateTime onlineThreshold)
    {
        var lastSeenAt = GetLastSeen(participationId);
        return lastSeenAt.HasValue && lastSeenAt.Value >= onlineThreshold;
    }

    public int CountOnline(IEnumerable<Guid> participationIds, DateTime onlineThreshold) =>
        participationIds.Distinct().Count(id => IsOnline(id, onlineThreshold));

    private sealed class PresenceEntry
    {
        public object SyncRoot { get; } = new();
        public HashSet<string> ConnectionIds { get; } = [];
        public DateTime LastSeenAt { get; set; }
    }
}
