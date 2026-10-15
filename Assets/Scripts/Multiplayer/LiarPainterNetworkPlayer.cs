using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class LiarPainterNetworkPlayer : NetworkBehaviour
{
    // Never broadcast actual hidden roles. Only the owner receives this assignment.
    public static event System.Action<LiarPainterPrivateAssignment> LocalAssignmentReceived;
    public static event System.Action<int, string, string> ChatReceived;
    public readonly NetworkVariable<int> ChatColorIndex = new(-1);
    private double nextChatTime;
    public LiarPainterPrivateAssignment LocalAssignment { get; private set; }
    public bool HasLocalAssignment { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetLocalEvents()
    {
        LocalAssignmentReceived = null;
        ChatReceived = null;
    }

    public bool SendChat(string message)
    {
        message = NormalizeChat(message);
        if (!IsOwner || !IsSpawned || message.Length == 0) return false;
        SendChatRpc(message);
        return true;
    }

    public static string NormalizeChat(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return string.Empty;
        var clean = new System.Text.StringBuilder();
        foreach (char c in message)
        {
            if (!char.IsControl(c)) clean.Append(c);
            if (clean.Length >= 120) break;
        }
        return clean.ToString().Trim();
    }

    [Rpc(SendTo.Server)]
    private void SendChatRpc(string message, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId || message == null || message.Length > 120) return;
        message = NormalizeChat(message);
        double now = NetworkManager.ServerTime.Time;
        if (message.Length == 0 || now < nextChatTime) return;
        nextChatTime = now + 0.3;
        string name = DisplayName.Value.ToString();
        ReceiveChatRpc(ChatColorIndex.Value, string.IsNullOrWhiteSpace(name) ? $"Player {OwnerClientId + 1}" : name, message);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ReceiveChatRpc(int colorIndex, string name, string message) => ChatReceived?.Invoke(colorIndex, name, message);

    public void AssignPrivateRoleServer(LiarPainterPrivateAssignment assignment)
    {
        if (!IsServer || !IsSpawned) return;
        ReceivePrivateRoleRpc((int)assignment.VisibleRole,
            new FixedString128Bytes(assignment.Keyword ?? string.Empty),
            RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ReceivePrivateRoleRpc(int visibleRole, FixedString128Bytes keyword, RpcParams rpcParams = default)
    {
        if (!IsOwner) return;
        LocalAssignment = new LiarPainterPrivateAssignment($"client-{OwnerClientId}",
            (LiarPainterRole)visibleRole, keyword.ToString());
        HasLocalAssignment = true;
        LocalAssignmentReceived?.Invoke(LocalAssignment);
    }

    public void ClearLocalAssignment()
    {
        LocalAssignment = default;
        HasLocalAssignment = false;
    }

    public readonly NetworkVariable<FixedString64Bytes> DisplayName = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            var used = new bool[8];
            foreach (var player in FindObjectsByType<LiarPainterNetworkPlayer>(FindObjectsSortMode.None))
                if (player != this && player.IsSpawned && player.ChatColorIndex.Value >= 0 && player.ChatColorIndex.Value < 8)
                    used[player.ChatColorIndex.Value] = true;
            for (int i = 0; i < used.Length; i++)
                if (!used[i]) { ChatColorIndex.Value = i; break; }
        }
        if (!IsOwner) return;
        string fallbackName = $"Player{OwnerClientId + 1}";
        if (fallbackName.Length > NicknamePopupController.MaximumNicknameLength)
            fallbackName = fallbackName.Substring(0, NicknamePopupController.MaximumNicknameLength);
        string nickname = NicknamePopupController.CurrentNickname;
        UpdateLocalNickname(string.IsNullOrWhiteSpace(nickname) ? fallbackName : nickname);
    }

    public void UpdateLocalNickname(string nickname)
    {
        if (!IsOwner || !IsSpawned || !NicknamePopupController.IsValidNickname(nickname)) return;
        SubmitDisplayNameRpc(new FixedString64Bytes(nickname));
    }

    [Rpc(SendTo.Server)]
    private void SubmitDisplayNameRpc(FixedString64Bytes displayName)
    {
        if (!NicknamePopupController.IsValidNickname(displayName.ToString())) return;
        DisplayName.Value = displayName;
        FindFirstObjectByType<LiarPainterNetworkGameCoordinator>()?.RefreshPlayerNameServer(OwnerClientId);
    }
}
