using System;
using System.Collections.Generic;

public enum LiarPainterRole
{
    Citizen,
    Appraiser,
    FakePainter,
    DrunkPainter
}

public enum LiarPainterTeam
{
    Citizen,
    FakePainter,
    Draw
}

public enum LiarPainterGamePhase
{
    Lobby,
    RoleReveal,
    QuestionReveal,
    Drawing,
    Discussion,
    Voting,
    Result,
    FinalDefense
}

// Shared game rules. Scene/Inspector values must never override these constants.
public static class LiarPainterRuleSettings
{
    public const float roleRevealSeconds = 8f;
    public const float questionRevealSeconds = 5f;
    public const float drawingSecondsPerPlayer = 15f;
    public const float discussionSeconds = 20f;
    public const float votingSeconds = 10f;
    public const float finalDefenseSeconds = 15f;
    public const int minimumPlayers = 4;
    public const int maximumPlayers = 8;
}

[Serializable]
public sealed class LiarPainterRoundContent
{
    // 실제 키워드 목록이 정해지기 전까지 Inspector 또는 서버에서 주입하는 변수입니다.
    public string citizenKeyword = string.Empty;
    public string drunkPainterKeyword = string.Empty;
    public string question = string.Empty;
}

[Serializable]
public sealed class LiarPainterPlayerInfo
{
    public string playerId;
    public string displayName;

    public LiarPainterPlayerInfo()
    {
    }

    public LiarPainterPlayerInfo(string id, string name)
    {
        playerId = id;
        displayName = name;
    }
}

public sealed class LiarPainterPlayerState
{
    public string PlayerId { get; }
    public string DisplayName { get; }
    public LiarPainterRole Role { get; internal set; }
    public string PrivateKeyword { get; internal set; }
    public IRoleAbility RoleAbility { get; internal set; }

    public LiarPainterTeam Team => Role == LiarPainterRole.FakePainter
        ? LiarPainterTeam.FakePainter
        : LiarPainterTeam.Citizen;

    public LiarPainterPlayerState(LiarPainterPlayerInfo info)
    {
        PlayerId = info.playerId;
        DisplayName = info.displayName;
        PrivateKeyword = string.Empty;
        RoleAbility = new NoRoleAbility();
    }
}

public readonly struct LiarPainterPrivateAssignment
{
    public string PlayerId { get; }
    public LiarPainterRole VisibleRole { get; }
    public string Keyword { get; }

    public LiarPainterPrivateAssignment(string playerId, LiarPainterRole visibleRole, string keyword)
    {
        PlayerId = playerId;
        VisibleRole = visibleRole;
        Keyword = keyword;
    }
}

public sealed class LiarPainterGameOutcome
{
    public LiarPainterTeam WinningTeam { get; }
    public string VotedPlayerId { get; }
    public bool WasTie { get; }
    public IReadOnlyList<LiarPainterPlayerState> Players { get; }

    public LiarPainterGameOutcome(LiarPainterTeam winningTeam, string votedPlayerId, bool wasTie, IReadOnlyList<LiarPainterPlayerState> players)
    {
        WinningTeam = winningTeam;
        VotedPlayerId = votedPlayerId;
        WasTie = wasTie;
        Players = players;
    }
}
