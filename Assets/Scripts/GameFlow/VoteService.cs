using System.Collections.Generic;
using System.Linq;

public sealed class VoteService
{
    private readonly Dictionary<string, string> votesByVoter = new();

    public void Clear()
    {
        votesByVoter.Clear();
    }

    public bool SubmitVote(string voterId, string targetPlayerId, IReadOnlyCollection<string> validPlayerIds)
    {
        if (string.IsNullOrWhiteSpace(voterId) || string.IsNullOrWhiteSpace(targetPlayerId)) return false;
        if (!validPlayerIds.Contains(voterId) || !validPlayerIds.Contains(targetPlayerId)) return false;

        votesByVoter[voterId] = targetPlayerId;

        return true;
    }

    public VoteResult CalculateResult()
    {
        if (votesByVoter.Count == 0) return new VoteResult(null, true, new Dictionary<string, int>());

        Dictionary<string, int> counts = votesByVoter.Values
            .GroupBy(target => target)
            .ToDictionary(group => group.Key, group => group.Count());

        int highest = counts.Values.Max();
        string[] leaders = counts.Where(pair => pair.Value == highest).Select(pair => pair.Key).ToArray();
        return new VoteResult(leaders.Length == 1 ? leaders[0] : null, leaders.Length != 1, counts);
    }
}

public readonly struct VoteResult
{
    public string VotedPlayerId { get; }
    public bool IsTie { get; }
    public IReadOnlyDictionary<string, int> Counts { get; }

    public VoteResult(string votedPlayerId, bool isTie, IReadOnlyDictionary<string, int> counts)
    {
        VotedPlayerId = votedPlayerId;
        IsTie = isTie;
        Counts = counts;
    }
}
