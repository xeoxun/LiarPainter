using System.Collections.Generic;
using System.Linq;

public static class WinConditionService
{
    public static LiarPainterGameOutcome Evaluate(
        VoteResult voteResult,
        IReadOnlyList<LiarPainterPlayerState> players,
        bool fakePainterWinsOnTie)
    {
        if (voteResult.IsTie)
        {
            LiarPainterTeam tieWinner = fakePainterWinsOnTie
                ? LiarPainterTeam.FakePainter
                : LiarPainterTeam.Citizen;
            return new LiarPainterGameOutcome(tieWinner, null, true, players);
        }

        LiarPainterPlayerState votedPlayer = players.FirstOrDefault(player => player.PlayerId == voteResult.VotedPlayerId);
        LiarPainterTeam winner = votedPlayer != null && votedPlayer.Role == LiarPainterRole.FakePainter
            ? LiarPainterTeam.Citizen
            : LiarPainterTeam.FakePainter;

        return new LiarPainterGameOutcome(winner, voteResult.VotedPlayerId, false, players);
    }
}
