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
    public readonly NetworkVariable<int> RoundNumber = new(0);
    public readonly NetworkVariable<FixedString64Bytes> DefenseName = new();
    private ulong defenseClient;
    private readonly HashSet<ulong> voteCandidates = new();
    private readonly HashSet<ulong> drawnThisRound = new();
    private readonly Dictionary<ulong, int> warnings = new();
    private readonly HashSet<ulong> receivedCandidates = new();
    private readonly Dictionary<ulong, int> receivedWarnings = new();
    public event Action RoundStateChanged;
    public bool IsVoteCandidate(ulong id) => receivedCandidates.Contains(id);
    public int GetMissedTurn(ulong id) => receivedWarnings.TryGetValue(id, out int turn) ? turn : 0;
    private Coroutine roundRoutine;
    private System.Random roundRandom;
    private DrawingCanvasLineRenderer drawingCanvas;
    private int nextStrokeId;
    private readonly Dictionary<(ulong client, int localStroke), int> liveStrokeIds = new();

    public bool LocalCanDraw { get; private set; }
    public event Action<string> AppraisalResultReceived;
    public event Action<int, string, bool> AppraisalCompleted;
    public event Action<string> ResultReceived;
    public event Action<bool> LocalDrawingPermissionChanged;
    public event Action<bool> DrawingTurnStarted;
    public event Action<ulong, bool> ReadyStateChanged;
    public event Action<ulong, string> PlayerRegistered;
    public event Action<ulong> PlayerLeft;
    public event Action ReturnedToWaitingRoom;
    public event Action GameInterrupted;
    public event Action VoteCountsChanged;
    public event Action<bool> KeywordGuessCompleted;

    public void SubmitKeywordGuess(string guess)
    {
        if (!IsSpawned || string.IsNullOrWhiteSpace(guess) || guess.Length > 32) return;
        SubmitKeywordGuessRpc(new FixedString128Bytes(guess.Trim()));
    }

    public static bool IsCorrectKeywordGuess(string guess, string keyword) =>
        !string.IsNullOrWhiteSpace(guess) && !string.IsNullOrWhiteSpace(keyword) &&
        string.Equals(guess.Trim().Normalize(), keyword.Trim().Normalize(), StringComparison.OrdinalIgnoreCase);

    [Rpc(SendTo.Server)]
    private void SubmitKeywordGuessRpc(FixedString128Bytes guess, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (Phase.Value != LiarPainterGamePhase.Discussion ||
            NetworkManager.ServerTime.Time >= PhaseEndsAt.Value || roundRoutine == null ||
            !playerStates.TryGetValue(sender, out var player) || player.Role != LiarPainterRole.FakePainter) return;
        bool correct = IsCorrectKeywordGuess(guess.ToString(), roundContent.citizenKeyword);
        KeywordGuessFeedbackRpc(correct, RpcTarget.Single(sender, RpcTargetUse.Temp));
        if (!correct) return;
        StopCoroutine(roundRoutine);
        roundRoutine = null;
        FinishGame(new LiarPainterGameOutcome(LiarPainterTeam.FakePainter, null, false, playerStates.Values.ToList()));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void KeywordGuessFeedbackRpc(bool correct, RpcParams rpcParams = default) => KeywordGuessCompleted?.Invoke(correct);
    private readonly Dictionary<ulong, int> receivedVoteCounts = new();
    public int GetReceivedVotes(ulong clientId) => receivedVoteCounts.TryGetValue(clientId, out int count) ? count : 0;

    public override void OnNetworkSpawn()
    {
        BindDrawingCanvas();
        if (IsServer)
        {
            NetworkManager.OnClientDisconnectCallback += HandleServerClientDisconnected;
            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
                readyByClient[clientId] = false;
        }

        RegisterPlayerRpc();
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
        drawingCanvas.NetworkStrokeUpdated += HandleLocalNetworkStroke;
        drawingCanvas.NetworkUndoRequested += HandleLocalNetworkErase;
    }

    private void UnbindDrawingCanvas()
    {
        if (drawingCanvas == null) return;
        drawingCanvas.NetworkStrokeCompleted -= HandleLocalNetworkStroke;
        drawingCanvas.NetworkStrokeUpdated -= HandleLocalNetworkStroke;
        drawingCanvas.NetworkUndoRequested -= HandleLocalNetworkErase;
        drawingCanvas = null;
    }

    private void HandleLocalNetworkStroke(NetworkDrawingStroke stroke)
    {
        if (!IsSpawned || stroke == null) return;
        SubmitStrokeRpc(JsonUtility.ToJson(stroke));
    }

    private void HandleLocalNetworkErase()
    {
        if (IsSpawned) UndoLatestStrokeRpc();
    }

    [Rpc(SendTo.Server)]
    private void SubmitStrokeRpc(string strokeJson, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!CanClientDraw(sender) || string.IsNullOrWhiteSpace(strokeJson) || strokeJson.Length > 24000) return;

        NetworkDrawingStroke stroke = JsonUtility.FromJson<NetworkDrawingStroke>(strokeJson);
        if (stroke?.points == null || stroke.points.Length < 2 || stroke.points.Length > 96) return;

        if (stroke.clientStrokeId == 0) return;
        var key = (sender, stroke.clientStrokeId);
        if (!liveStrokeIds.TryGetValue(key, out int strokeId))
        {
            strokeId = ++nextStrokeId;
            liveStrokeIds[key] = strokeId;
        }
        else if (!strokeOwners.ContainsKey(strokeId)) return; // Do not resurrect erased strokes.
        stroke.strokeId = strokeId;
        stroke.ownerPlayerId = PlayerId(sender);
        stroke.width = Mathf.Clamp(stroke.width, 0.001f, 1f);
        for (int i = 0; i < stroke.points.Length; i++)
            stroke.points[i] = Vector3.ClampMagnitude(stroke.points[i], 100f);

        if (!RegisterStrokeOwner(stroke.strokeId, sender)) return;
        drawnThisRound.Add(sender);
        ApplyStrokeRpc(JsonUtility.ToJson(stroke));
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ApplyStrokeRpc(string strokeJson)
    {
        NetworkDrawingStroke stroke = JsonUtility.FromJson<NetworkDrawingStroke>(strokeJson);
        drawingCanvas?.ApplyNetworkStroke(stroke);
    }

    [Rpc(SendTo.Server)]
    private void UndoLatestStrokeRpc(RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!CanClientDraw(sender)) return;
        string owner = PlayerId(sender);
        int strokeId = 0;
        foreach (var entry in strokeOwners)
            if (entry.Value == owner && entry.Key > strokeId) strokeId = entry.Key;
        if (strokeId == 0) return;
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
        if (NetworkManager.ServerTime.Time >= PhaseEndsAt.Value) return false;
        if (Phase.Value == LiarPainterGamePhase.FinalDefense) return clientId == defenseClient;
        int index = AnonymousTurn.Value - 1;
        return Phase.Value == LiarPainterGamePhase.Drawing &&
               index >= 0 && index < drawingOrder.Count && drawingOrder[index] == clientId;
    }

    [Rpc(SendTo.Server)]
    private void RegisterPlayerRpc(RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        displayNames[sender] = GetPlayerName(sender);
        if (!readyByClient.ContainsKey(sender)) readyByClient[sender] = false;
        foreach (KeyValuePair<ulong, string> player in displayNames)
            PlayerRegisteredRpc(player.Key, new FixedString64Bytes(player.Value), RpcTarget.Single(sender, RpcTargetUse.Temp));
        PlayerRegisteredRpc(sender, new FixedString64Bytes(displayNames[sender]), RpcTarget.Not(sender, RpcTargetUse.Temp));
    }

    private string GetPlayerName(ulong clientId)
    {
        if (NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) &&
            client.PlayerObject != null && client.PlayerObject.TryGetComponent(out LiarPainterNetworkPlayer player) &&
            !player.DisplayName.Value.IsEmpty)
            return player.DisplayName.Value.ToString();
        return $"Player {clientId + 1}";
    }

    public void RefreshPlayerNameServer(ulong clientId)
    {
        if (!IsServer || !IsSpawned || !displayNames.ContainsKey(clientId)) return;
        displayNames[clientId] = GetPlayerName(clientId);
        foreach (ulong target in NetworkManager.ConnectedClientsIds)
            PlayerRegisteredRpc(clientId, new FixedString64Bytes(displayNames[clientId]), RpcTarget.Single(target, RpcTargetUse.Temp));
        if (readyByClient.TryGetValue(clientId, out bool ready)) ReadyChangedRpc(clientId, ready);
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
        if (connected.Length < LiarPainterRuleSettings.minimumPlayers || connected.Length > LiarPainterRuleSettings.maximumPlayers) return;
        if (connected.Any(id => !readyByClient.TryGetValue(id, out bool ready) || !ready)) return;
        if (connected.Any(id => NetworkManager.ConnectedClients[id].PlayerObject == null ||
            !NetworkManager.ConnectedClients[id].PlayerObject.TryGetComponent<LiarPainterNetworkPlayer>(out _)))
        {
            Debug.LogWarning("모든 참가자의 NetworkPlayer 생성이 완료되어야 게임을 시작할 수 있습니다.");
            return;
        }
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
        if (Phase.Value != LiarPainterGamePhase.Voting ||
            NetworkManager.ServerTime.Time >= PhaseEndsAt.Value ||
            !playerStates.ContainsKey(rpcParams.Receive.SenderClientId) ||
            !playerStates.ContainsKey(targetClientId) || !voteCandidates.Contains(targetClientId)) return;
        if (voteService.SubmitVote(PlayerId(rpcParams.Receive.SenderClientId), PlayerId(targetClientId), clientByPlayerId.Keys.ToArray()))
            BroadcastVoteCounts();
    }

    private void BroadcastVoteCounts()
    {
        var counts = voteService.CalculateResult().Counts;
        ulong[] clients = playerStates.Keys.ToArray();
        int[] totals = clients.Select(id => counts.TryGetValue(PlayerId(id), out int count) ? count : 0).ToArray();
        VoteCountsRpc(clients, totals);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void VoteCountsRpc(ulong[] clients, int[] totals)
    {
        receivedVoteCounts.Clear();
        for (int i = 0; i < clients.Length && i < totals.Length; i++) receivedVoteCounts[clients[i]] = totals[i];
        VoteCountsChanged?.Invoke();
    }

    [Rpc(SendTo.Server)]
    private void RequestAppraisalRpc(int strokeId, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!playerStates.TryGetValue(sender, out LiarPainterPlayerState player) ||
            !strokeOwners.TryGetValue(strokeId, out string ownerId) ||
            Phase.Value != LiarPainterGamePhase.Discussion || NetworkManager.ServerTime.Time >= PhaseEndsAt.Value)
        {
            AppraisalFeedbackRpc(strokeId, "", false, RpcTarget.Single(sender, RpcTargetUse.Temp));
            return;
        }

        RoleAbilityResult result = player.RoleAbility.TryUse(new RoleAbilityContext(Phase.Value, strokeId, ownerId));
        if (result.Succeeded)
        {
            AppraisalResultRpc(new FixedString64Bytes(result.RevealedPlayerId), RpcTarget.Single(sender, RpcTargetUse.Temp));
            string name = clientByPlayerId.TryGetValue(ownerId, out ulong ownerClient) && displayNames.TryGetValue(ownerClient, out string displayName)
                ? displayName : ownerId;
            AppraisalFeedbackRpc(strokeId, name, true, RpcTarget.Single(sender, RpcTargetUse.Temp));
        }
        else AppraisalFeedbackRpc(strokeId, "", false, RpcTarget.Single(sender, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void AppraisalFeedbackRpc(int strokeId, string displayName, bool succeeded, RpcParams rpcParams = default)
        => AppraisalCompleted?.Invoke(strokeId, displayName, succeeded);

    [Rpc(SendTo.SpecifiedInParams)]
    private void AppraisalResultRpc(FixedString64Bytes ownerPlayerId, RpcParams rpcParams = default)
    {
        AppraisalResultReceived?.Invoke(ownerPlayerId.ToString());
    }

    private IEnumerator RunRound(IReadOnlyList<ulong> clients)
    {
        var sessions = LiarPainterSessionManager.Instance;
        if (sessions != null && sessions.CurrentSession != null)
        {
            var locking = sessions.SetRoomPlayingAsync(true);
            while (!locking.IsCompleted) yield return null;
            if (!locking.Result || clients.Count != NetworkManager.ConnectedClientsIds.Count ||
                clients.Any(id => !NetworkManager.ConnectedClients.ContainsKey(id) ||
                    !readyByClient.TryGetValue(id, out bool ready) || !ready))
            {
                ResetToWaitingRoom();
                yield break;
            }
        }
        BuildServerRoundState(clients);
        ClearDrawingRpc();

        SetPhase(LiarPainterGamePhase.RoleReveal, LiarPainterRuleSettings.roleRevealSeconds);
        foreach (ulong clientId in clients)
        {
            LiarPainterPrivateAssignment assignment = GetPrivateAssignment(clientId);
            if (NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) &&
                client.PlayerObject != null &&
                client.PlayerObject.TryGetComponent(out LiarPainterNetworkPlayer player))
            {
                player.AssignPrivateRoleServer(assignment);
                continue;
            }
            Debug.LogError($"플레이어 {clientId}의 NetworkPlayer 프리팹이 없습니다.");
        }
        yield return WaitForPhase();

        for (int round = 1; round <= 2; round++)
        {
            RoundNumber.Value = round;
            if (round == 2)
            {
                var questions = contentCatalog.Questions.Where(q => !string.IsNullOrWhiteSpace(q) && q != roundContent.question).ToArray();
                if (questions.Length > 0) roundContent.question = questions[roundRandom.Next(questions.Length)];
                RoleAssignmentService.Shuffle(drawingOrder, roundRandom);
            }
            foreach (var state in playerStates.Values) state.RoleAbility = RoleAbilityFactory.Create(state.Role);
            drawnThisRound.Clear();
            warnings.Clear();
            strokeOwners.Clear();
            liveStrokeIds.Clear();
            ClearDrawingRpc();
            BroadcastRoundState();
            Question.Value = new FixedString128Bytes(roundContent.question);
            SetPhase(LiarPainterGamePhase.QuestionReveal, LiarPainterRuleSettings.questionRevealSeconds);
            yield return WaitForPhase();

            DrawingPlayerCount.Value = drawingOrder.Count;
            for (int i = 0; i < drawingOrder.Count; i++)
            {
                AnonymousTurn.Value = i + 1;
                SetPhase(LiarPainterGamePhase.Drawing, LiarPainterRuleSettings.drawingSecondsPerPlayer);
                SetDrawingPermissionRpc(true, RpcTarget.Single(drawingOrder[i], RpcTargetUse.Temp));
                foreach (ulong clientId in clients)
                    DrawingTurnStartedRpc(clientId == drawingOrder[i], RpcTarget.Single(clientId, RpcTargetUse.Temp));
                yield return WaitForPhase();
                SetDrawingPermissionRpc(false, RpcTarget.Single(drawingOrder[i], RpcTargetUse.Temp));
                if (!drawnThisRound.Contains(drawingOrder[i])) warnings[drawingOrder[i]] = i + 1;
                BroadcastRoundState();
            }
            AnonymousTurn.Value = 0;
            SetPhase(LiarPainterGamePhase.Discussion, LiarPainterRuleSettings.discussionSeconds);
            yield return WaitForPhase();
        }

        voteCandidates.Clear();
        foreach (ulong client in clients) voteCandidates.Add(client);
        VoteResult voteResult;
        while (true)
        {
            voteService.Clear();
            BroadcastRoundState();
            SetPhase(LiarPainterGamePhase.Voting, LiarPainterRuleSettings.votingSeconds);
            BroadcastVoteCounts();
            yield return WaitForPhase();
            voteResult = voteService.CalculateResult();
            if (voteResult.Counts.Count == 0 || !voteResult.IsTie) break;
            int highest = voteResult.Counts.Values.Max();
            voteCandidates.Clear();
            foreach (var entry in voteResult.Counts)
                if (entry.Value == highest) voteCandidates.Add(clientByPlayerId[entry.Key]);
        }

        if (voteResult.Counts.Count > 0)
        {
            defenseClient = clientByPlayerId[voteResult.VotedPlayerId];
            DefenseName.Value = new FixedString64Bytes(displayNames[defenseClient]);
            SetPhase(LiarPainterGamePhase.FinalDefense, LiarPainterRuleSettings.finalDefenseSeconds);
            SetDrawingPermissionRpc(true, RpcTarget.Single(defenseClient, RpcTargetUse.Temp));
            yield return WaitForPhase();
            SetDrawingPermissionRpc(false, RpcTarget.Single(defenseClient, RpcTargetUse.Temp));
        }

        var outcome = WinConditionService.Evaluate(voteResult, playerStates.Values.ToList());
        FinishGame(outcome);
    }

    private void FinishGame(LiarPainterGameOutcome outcome)
    {
        SetPhase(LiarPainterGamePhase.Result, 0);
        string resultJson = JsonUtility.ToJson(CreateResultPayload(outcome));
        ResetToWaitingRoom();
        ResultRpc(resultJson);
    }

    private void BroadcastRoundState()
    {
        RoundStateRpc(voteCandidates.ToArray(), warnings.Keys.ToArray(), warnings.Values.ToArray());
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void RoundStateRpc(ulong[] candidates, ulong[] players, int[] turns)
    {
        receivedCandidates.Clear();
        foreach (ulong id in candidates) receivedCandidates.Add(id);
        receivedWarnings.Clear();
        for (int i = 0; i < players.Length && i < turns.Length; i++) receivedWarnings[players[i]] = turns[i];
        RoundStateChanged?.Invoke();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ClearDrawingRpc()
    {
        drawingCanvas?.ClearCanvas();
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void DrawingTurnStartedRpc(bool isMyTurn, RpcParams rpcParams = default)
        => DrawingTurnStarted?.Invoke(isMyTurn);

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
        liveStrokeIds.Clear();
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
        if (LiarPainterSessionManager.Instance != null)
            _ = LiarPainterSessionManager.Instance.SetRoomPlayingAsync(false);
        voteService.Clear();
        voteCandidates.Clear();
        warnings.Clear();
        BroadcastRoundState();
        RoundNumber.Value = 0;
        DefenseName.Value = default;
        BroadcastVoteCounts();
        ClearDrawingRpc();
        strokeOwners.Clear();
        liveStrokeIds.Clear();
        nextStrokeId = 0;
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
            GameInterruptedRpc();
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void GameInterruptedRpc() => GameInterrupted?.Invoke();

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
