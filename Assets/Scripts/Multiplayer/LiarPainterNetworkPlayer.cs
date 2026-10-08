using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class LiarPainterNetworkPlayer : NetworkBehaviour
{
    public readonly NetworkVariable<FixedString64Bytes> DisplayName = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;
        string fallbackName = $"Player {OwnerClientId + 1}";
        SubmitDisplayNameRpc(new FixedString64Bytes(
            PlayerPrefs.GetString("LiarPainter.PlayerName", fallbackName)));
    }

    [Rpc(SendTo.Server)]
    private void SubmitDisplayNameRpc(FixedString64Bytes displayName)
    {
        DisplayName.Value = displayName;
    }
}
