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
    FakePainter
}

public enum LiarPainterGamePhase
{
    Lobby,
    RoleReveal,
    QuestionReveal,
    Drawing,
    Discussion,
    Voting,
    Result
}

[Serializable]
public sealed class LiarPainterRuleSettings
{
    public float roleRevealSeconds = 5f;
    public float questionRevealSeconds = 5f;
    public float drawingSecondsPerPlayer = 8f;
    public float discussionSeconds = 180f;
    public float votingSeconds = 10f;
    public float resultSeconds = 7f;
    public int minimumPlayers = 4;
    public int maximumPlayers = 8;
    public bool fakePainterWinsOnVoteTie = true;
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

    public LiarPainterGameOutcome(
        LiarPainterTeam winningTeam,
        string votedPlayerId,
        bool wasTie,
        IReadOnlyList<LiarPainterPlayerState> players)
    {
        WinningTeam = winningTeam;
        VotedPlayerId = votedPlayerId;
        WasTie = wasTie;
        Players = players;
    }
}
