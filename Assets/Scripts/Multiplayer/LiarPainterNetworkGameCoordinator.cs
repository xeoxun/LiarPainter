using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public sealed class LiarPainterNetworkGameCoordinator : NetworkBehaviour
{
    [SerializeField] private LiarPainterRuleSettings rules = new();
    [SerializeField] private LiarPainterRoundContent roundContent = new();
    [SerializeField] private LiarPainterContentCatalog contentCatalog;

    public readonly NetworkVariable<LiarPainterGamePhase> Phase = new(LiarPainterGamePhase.Lobby);
    public readonly NetworkVariable<double> PhaseEndsAt = new(0d);
    public readonly NetworkVariable<FixedString128Bytes> Question = new();
    public readonly NetworkVariable<int> AnonymousTurn = new(0);
    public readonly NetworkVariable<int> DrawingPlayerCount = new(0);

    private readonly Dictionary<ulong, string> displayNames = new();
    private readonly Dictionary<ulong, bool> readyByClient = new();
    private readonly Dictionary<ulong, LiarPainterPlayerState> playerStates = new();
    private readonly Dictionary<string, ulong> clientByPlayerId = new();
    private readonly Dictionary<int, string> strokeOwners = new();
    private readonly List<ulong> drawingOrder = new();
    private readonly VoteService voteService = new();
    private Coroutine roundRoutine;
    private System.Random roundRandom;
    private DrawingCanvasLineRenderer drawingCanvas;
    private int nextStrokeId;

    public bool LocalCanDraw { get; private set; }
    public event Action<LiarPainterPrivateAssignment> PrivateAssignmentReceived;
    public event Action<string> AppraisalResultReceived;
    public event Action<string> ResultReceived;
    public event Action<bool> LocalDrawingPermissionChanged;
    public event Action<ulong, bool> ReadyStateChanged;
    public event Action<ulong, string> PlayerRegistered;
    public event Action<ulong> PlayerLeft;
    public event Action ReturnedToWaitingRoom;

    public override void OnNetworkSpawn()
    {
        BindDrawingCanvas();
        if (IsServer)
        {
            NetworkManager.OnClientDisconnectCallback += HandleServerClientDisconnected;
            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
                readyByClient[clientId] = false;
        }

        string displayName = PlayerPrefs.GetString("LiarPainter.PlayerName", $"Player {NetworkManager.LocalClientId + 1}");
        RegisterPlayerRpc(new FixedString64Bytes(displayName));
    }

    public override void OnNetworkDespawn()
    {
        UnbindDrawingCanvas();
        if (NetworkManager != null)
            NetworkManager.OnClientDisconnectCallback -= HandleServerClientDisconnected;
    }

    public void SetLocalReady(bool ready)
    {
        if (IsSpawned) SetReadyRpc(ready);
    }

    public void RequestStartGame()
    {
        if (IsSpawned) RequestStartGameRpc();
    }

    public void SubmitVote(ulong targetClientId)
    {
        if (IsSpawned) SubmitVoteRpc(targetClientId);
    }

    public void RequestAppraisal(int strokeId)
    {
        if (IsSpawned) RequestAppraisalRpc(strokeId);
    }

    // 그림 동기화 계층이 새 선을 승인한 직후 서버에서 호출합니다.
    public bool RegisterStrokeOwner(int strokeId, ulong ownerClientId)
    {
        if (!IsServer || strokeId <= 0 || !clientByPlayerId.ContainsValue(ownerClientId)) return false;
        strokeOwners[strokeId] = PlayerId(ownerClientId);
        return true;
    }

    private void BindDrawingCanvas()
    {
        drawingCanvas = GetComponentInChildren<DrawingCanvasLineRenderer>(true);
        if (drawingCanvas == null) drawingCanvas = FindFirstObjectByType<DrawingCanvasLineRenderer>();
        if (drawingCanvas == null) return;
        drawingCanvas.NetworkStrokeCompleted += HandleLocalNetworkStroke;
        drawingCanvas.NetworkStrokeEraseRequested += HandleLocalNetworkErase;
    }

    private void UnbindDrawingCanvas()
    {
        if (drawingCanvas == null) return;
        drawingCanvas.NetworkStrokeCompleted -= HandleLocalNetworkStroke;
        drawingCanvas.NetworkStrokeEraseRequested -= HandleLocalNetworkErase;
        drawingCanvas = null;
    }

    private void HandleLocalNetworkStroke(NetworkDrawingStroke stroke)
    {
        if (!IsSpawned || stroke == null) return;
        SubmitStrokeRpc(JsonUtility.ToJson(stroke));
    }

    private void HandleLocalNetworkErase(int strokeId)
    {
        if (IsSpawned && strokeId > 0) EraseStrokeRpc(strokeId);
    }

    [Rpc(SendTo.Server)]
    private void SubmitStrokeRpc(string strokeJson, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!CanClientDraw(sender) || string.IsNullOrWhiteSpace(strokeJson) || strokeJson.Length > 24000) return;

        NetworkDrawingStroke stroke = JsonUtility.FromJson<NetworkDrawingStroke>(strokeJson);
        if (stroke?.points == null || stroke.points.Length < 2 || stroke.points.Length > 96) return;

        stroke.strokeId = ++nextStrokeId;
        stroke.ownerPlayerId = PlayerId(sender);
        stroke.width = Mathf.Clamp(stroke.width, 0.001f, 1f);
        for (int i = 0; i < stroke.points.Length; i++)
            stroke.points[i] = Vector3.ClampMagnitude(stroke.points[i], 100f);

        if (!RegisterStrokeOwner(stroke.strokeId, sender)) return;
        ApplyStrokeRpc(JsonUtility.ToJson(stroke));
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ApplyStrokeRpc(string strokeJson)
    {
        NetworkDrawingStroke stroke = JsonUtility.FromJson<NetworkDrawingStroke>(strokeJson);
        drawingCanvas?.ApplyNetworkStroke(stroke);
    }

    [Rpc(SendTo.Server)]
    private void EraseStrokeRpc(int strokeId, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!CanClientDraw(sender) || !strokeOwners.TryGetValue(strokeId, out string owner) || owner != PlayerId(sender)) return;
        strokeOwners.Remove(strokeId);
        RemoveStrokeRpc(strokeId);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void RemoveStrokeRpc(int strokeId)
    {
        drawingCanvas?.RemoveNetworkStroke(strokeId);
    }

    private bool CanClientDraw(ulong clientId)
    {
        int index = AnonymousTurn.Value - 1;
        return Phase.Value == LiarPainterGamePhase.Drawing &&
               index >= 0 && index < drawingOrder.Count && drawingOrder[index] == clientId;
    }

    [Rpc(SendTo.Server)]
    private void RegisterPlayerRpc(FixedString64Bytes displayName, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        displayNames[sender] = displayName.IsEmpty ? $"Player {sender + 1}" : displayName.ToString();
        if (!readyByClient.ContainsKey(sender)) readyByClient[sender] = false;
        foreach (KeyValuePair<ulong, string> player in displayNames)
            PlayerRegisteredRpc(player.Key, new FixedString64Bytes(player.Value), RpcTarget.Single(sender, RpcTargetUse.Temp));
        PlayerRegisteredRpc(sender, new FixedString64Bytes(displayNames[sender]), RpcTarget.Not(sender, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void PlayerRegisteredRpc(ulong clientId, FixedString64Bytes displayName, RpcParams rpcParams = default)
    {
        PlayerRegistered?.Invoke(clientId, displayName.ToString());
    }

    [Rpc(SendTo.Server)]
    private void SetReadyRpc(bool ready, RpcParams rpcParams = default)
    {
        if (Phase.Value != LiarPainterGamePhase.Lobby) return;
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!NetworkManager.ConnectedClients.ContainsKey(sender)) return;
        readyByClient[sender] = ready;
        ReadyChangedRpc(sender, ready);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ReadyChangedRpc(ulong clientId, bool ready)
    {
        ReadyStateChanged?.Invoke(clientId, ready);
    }

    [Rpc(SendTo.Server)]
    private void RequestStartGameRpc(RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId ||
            Phase.Value != LiarPainterGamePhase.Lobby || roundRoutine != null) return;

        ulong[] connected = NetworkManager.ConnectedClientsIds.ToArray();
        if (connected.Length < rules.minimumPlayers || connected.Length > rules.maximumPlayers) return;
        if (connected.Any(id => !readyByClient.TryGetValue(id, out bool ready) || !ready)) return;
        roundRandom = new System.Random();
        if (!TryGenerateRoundContent(roundRandom)) return;
        if (string.IsNullOrWhiteSpace(roundContent.citizenKeyword) ||
            string.IsNullOrWhiteSpace(roundContent.drunkPainterKeyword) ||
            string.IsNullOrWhiteSpace(roundContent.question)) return;

        roundRoutine = StartCoroutine(RunRound(connected));
    }

    [Rpc(SendTo.Server)]
    private void SubmitVoteRpc(ulong targetClientId, RpcParams rpcParams = default)
    {
        if (Phase.Value != LiarPainterGamePhase.Voting || !playerStates.ContainsKey(targetClientId)) return;
        voteService.SubmitVote(PlayerId(rpcParams.Receive.SenderClientId), PlayerId(targetClientId), clientByPlayerId.Keys.ToArray());
    }

    [Rpc(SendTo.Server)]
    private void RequestAppraisalRpc(int strokeId, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!playerStates.TryGetValue(sender, out LiarPainterPlayerState player) ||
            !strokeOwners.TryGetValue(strokeId, out string ownerId)) return;

        RoleAbilityResult result = player.RoleAbility.TryUse(new RoleAbilityContext(Phase.Value, strokeId, ownerId));
        if (result.Succeeded)
            AppraisalResultRpc(new FixedString64Bytes(result.RevealedPlayerId), RpcTarget.Single(sender, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void AppraisalResultRpc(FixedString64Bytes ownerPlayerId, RpcParams rpcParams = default)
    {
        AppraisalResultReceived?.Invoke(ownerPlayerId.ToString());
    }

    private IEnumerator RunRound(IReadOnlyList<ulong> clients)
    {
        BuildServerRoundState(clients);
        ClearDrawingRpc();

        SetPhase(LiarPainterGamePhase.RoleReveal, rules.roleRevealSeconds);
        foreach (ulong clientId in clients)
        {
            LiarPainterPrivateAssignment assignment = GetPrivateAssignment(clientId);
            PrivateAssignmentRpc(
                (int)assignment.VisibleRole,
                new FixedString128Bytes(assignment.Keyword ?? string.Empty),
                RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }
        yield return WaitForPhase();

        Question.Value = new FixedString128Bytes(roundContent.question);
        SetPhase(LiarPainterGamePhase.QuestionReveal, rules.questionRevealSeconds);
        yield return WaitForPhase();

        DrawingPlayerCount.Value = drawingOrder.Count;
        for (int i = 0; i < drawingOrder.Count; i++)
        {
            SetPhase(LiarPainterGamePhase.Drawing, rules.drawingSecondsPerPlayer);
            AnonymousTurn.Value = i + 1;
            SetDrawingPermissionRpc(true, RpcTarget.Single(drawingOrder[i], RpcTargetUse.Temp));
            yield return WaitForPhase();
            SetDrawingPermissionRpc(false, RpcTarget.Single(drawingOrder[i], RpcTargetUse.Temp));
        }

        AnonymousTurn.Value = 0;
        SetPhase(LiarPainterGamePhase.Discussion, rules.discussionSeconds);
        yield return WaitForPhase();

        voteService.Clear();
        SetPhase(LiarPainterGamePhase.Voting, rules.votingSeconds);
        yield return WaitForPhase();

        VoteResult voteResult = voteService.CalculateResult();
        LiarPainterGameOutcome outcome = WinConditionService.Evaluate(voteResult, playerStates.Values.ToList(), rules.fakePainterWinsOnVoteTie);
        SetPhase(LiarPainterGamePhase.Result, rules.resultSeconds);
        ResultRpc(JsonUtility.ToJson(CreateResultPayload(outcome)));
        yield return WaitForPhase();

        ResetToWaitingRoom();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ClearDrawingRpc()
    {
        drawingCanvas?.ClearCanvas();
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void PrivateAssignmentRpc(int visibleRole, FixedString128Bytes keyword, RpcParams rpcParams = default)
    {
        PrivateAssignmentReceived?.Invoke(new LiarPainterPrivateAssignment(
            PlayerId(NetworkManager.LocalClientId),
            (LiarPainterRole)visibleRole,
            keyword.ToString()));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void SetDrawingPermissionRpc(bool allowed, RpcParams rpcParams = default)
    {
        LocalCanDraw = allowed;
        LocalDrawingPermissionChanged?.Invoke(allowed);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ResultRpc(string resultJson)
    {
        ResultReceived?.Invoke(resultJson);
    }

    private void BuildServerRoundState(IReadOnlyList<ulong> clients)
    {
        playerStates.Clear();
        clientByPlayerId.Clear();
        drawingOrder.Clear();
        strokeOwners.Clear();
        nextStrokeId = 0;
        voteService.Clear();

        System.Random random = roundRandom ?? new System.Random();
        IReadOnlyList<LiarPainterRole> roles = RoleAssignmentService.CreateRoles(clients.Count, random);
        for (int i = 0; i < clients.Count; i++)
        {
            ulong clientId = clients[i];
            string playerId = PlayerId(clientId);
            LiarPainterPlayerState state = new(new LiarPainterPlayerInfo(
                playerId,
                displayNames.TryGetValue(clientId, out string name) ? name : $"Player {clientId + 1}"))
            {
                Role = roles[i],
                RoleAbility = RoleAbilityFactory.Create(roles[i])
            };
            state.PrivateKeyword = KeywordAssignmentService.GetPrivateKeyword(state.Role, roundContent);
            playerStates[clientId] = state;
            clientByPlayerId[playerId] = clientId;
            drawingOrder.Add(clientId);
        }
        RoleAssignmentService.Shuffle(drawingOrder, random);
    }

    private bool TryGenerateRoundContent(System.Random sourceRandom)
    {
        if (contentCatalog == null) contentCatalog = LiarPainterContentProvider.LoadDefault();
        if (contentCatalog != null && contentCatalog.TryCreateRound(sourceRandom, out LiarPainterRoundContent generated))
        {
            roundContent = generated;
            return true;
        }

        Debug.LogWarning("서버가 LiarPainterContentCatalog에서 라운드 콘텐츠를 생성하지 못했습니다.");
        return false;
    }

    private LiarPainterPrivateAssignment GetPrivateAssignment(ulong clientId)
    {
        LiarPainterPlayerState state = playerStates[clientId];
        return new LiarPainterPrivateAssignment(state.PlayerId, KeywordAssignmentService.GetVisibleRole(state.Role), state.PrivateKeyword);
    }

    private void SetPhase(LiarPainterGamePhase phase, float seconds)
    {
        Phase.Value = phase;
        PhaseEndsAt.Value = NetworkManager.ServerTime.Time + Mathf.Max(0f, seconds);
    }

    private IEnumerator WaitForPhase()
    {
        while (NetworkManager != null && NetworkManager.IsListening && NetworkManager.ServerTime.Time < PhaseEndsAt.Value)
            yield return null;
    }

    private void ResetToWaitingRoom()
    {
        Phase.Value = LiarPainterGamePhase.Lobby;
        PhaseEndsAt.Value = 0d;
        Question.Value = default;
        AnonymousTurn.Value = 0;
        DrawingPlayerCount.Value = 0;
        roundRoutine = null;
        foreach (ulong clientId in readyByClient.Keys.ToArray())
        {
            readyByClient[clientId] = false;
            ReadyChangedRpc(clientId, false);
        }
        ReturnedToWaitingRoomRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ReturnedToWaitingRoomRpc()
    {
        ReturnedToWaitingRoom?.Invoke();
    }

    private void HandleServerClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;
        displayNames.Remove(clientId);
        readyByClient.Remove(clientId);
        PlayerLeftRpc(clientId);
        if (roundRoutine != null)
        {
            StopCoroutine(roundRoutine);
            roundRoutine = null;
            ResetToWaitingRoom();
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlayerLeftRpc(ulong clientId)
    {
        PlayerLeft?.Invoke(clientId);
    }

    private static string PlayerId(ulong clientId) => $"client-{clientId}";

    private static NetworkResultPayload CreateResultPayload(LiarPainterGameOutcome outcome)
    {
        return new NetworkResultPayload
        {
            winningTeam = outcome.WinningTeam,
            votedPlayerId = outcome.VotedPlayerId ?? string.Empty,
            wasTie = outcome.WasTie,
            players = outcome.Players.Select(player => new NetworkResultPlayer
            {
                playerId = player.PlayerId,
                displayName = player.DisplayName,
                role = player.Role,
                keyword = player.PrivateKeyword,
                won = player.Team == outcome.WinningTeam
            }).ToArray()
        };
    }
}

[Serializable]
public sealed class NetworkResultPayload
{
    public LiarPainterTeam winningTeam;
    public string votedPlayerId;
    public bool wasTie;
    public NetworkResultPlayer[] players;
}

[Serializable]
public sealed class NetworkResultPlayer
{
    public string playerId;
    public string displayName;
    public LiarPainterRole role;
    public string keyword;
    public bool won;
}
