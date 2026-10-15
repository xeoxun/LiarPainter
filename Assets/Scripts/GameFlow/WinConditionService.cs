using System.Collections.Generic;
using System.Linq;

public static class WinConditionService
{
    public static LiarPainterGameOutcome Evaluate(VoteResult voteResult, IReadOnlyList<LiarPainterPlayerState> players)
    {
        if (voteResult.IsTie)
        {
            return new LiarPainterGameOutcome(LiarPainterTeam.Draw, null, true, players);
        }

        LiarPainterPlayerState votedPlayer = players.FirstOrDefault(player => player.PlayerId == voteResult.VotedPlayerId);

        LiarPainterTeam winner = votedPlayer != null && votedPlayer.Role == LiarPainterRole.FakePainter ? LiarPainterTeam.Citizen : LiarPainterTeam.Draw;

        return new LiarPainterGameOutcome(winner, voteResult.VotedPlayerId, false, players);
    }
}
