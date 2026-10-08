using System;
using System.Linq;
using UnityEngine;
using Unity.Netcode;

public sealed class LobbyReadyController : MonoBehaviour
{
    [SerializeField, Min(4)] private int minimumPlayersToStart = 4;
    [SerializeField] private PlayerSlotView[] playerSlots;
    private LiarPainterNetworkGameCoordinator networkCoordinator;

    public event Action<bool> LocalReadyChanged;
    public event Action HostStartRequested;

    public bool AreAllOccupiedPlayersReady =>
        playerSlots != null &&
        playerSlots.Count(slot => slot != null && slot.IsOccupied) >= minimumPlayersToStart &&
        playerSlots.Where(slot => slot != null && slot.IsOccupied).All(slot => slot.IsReady);

    private void Awake()
    {
        RefreshSlots();
    }

    private void OnEnable()
    {
        TryBindNetworkCoordinator();
    }

    private void OnDestroy()
    {
        UnbindNetworkCoordinator();
        Unsubscribe();
    }

    public void RefreshSlots()
    {
        Unsubscribe();
        playerSlots = GetComponentsInChildren<PlayerSlotView>(true);

        foreach (PlayerSlotView slot in playerSlots)
        {
            slot.ReadyButtonClicked += HandleReadyClicked;
            slot.StartGameButtonClicked += HandleStartClicked;
        }

        RefreshStartButton();
    }

    public void ApplyNetworkReadyState(PlayerSlotView slot, bool isReady)
    {
        if (slot == null) return;
        slot.SetReadyState(isReady);
        RefreshStartButton();
    }

    public void RefreshStartButton()
    {
        bool canStart = AreAllOccupiedPlayersReady;
        if (playerSlots == null) return;

        foreach (PlayerSlotView slot in playerSlots)
        {
            slot.SetStartButtonInteractable(canStart && slot.IsHost);
        }
    }

    private void HandleReadyClicked(PlayerSlotView slot)
    {
        bool requestedState = !slot.IsReady;
        LocalReadyChanged?.Invoke(requestedState);

        if (networkCoordinator != null && networkCoordinator.IsSpawned)
        {
            networkCoordinator.SetLocalReady(requestedState);
            return;
        }

        // 네트워크가 연결되기 전에도 UI를 확인할 수 있는 로컬 동작입니다.
        // 멀티플레이에서는 서버가 승인한 뒤 ApplyNetworkReadyState를 호출하면 됩니다.
        ApplyNetworkReadyState(slot, requestedState);
    }

    private void HandleStartClicked(PlayerSlotView slot)
    {
        if (!slot.IsHost || !AreAllOccupiedPlayersReady) return;
        HostStartRequested?.Invoke();
        if (networkCoordinator != null && networkCoordinator.IsSpawned)
            networkCoordinator.RequestStartGame();
    }

    private void TryBindNetworkCoordinator()
    {
        LiarPainterNetworkGameCoordinator found = FindFirstObjectByType<LiarPainterNetworkGameCoordinator>();
        if (found == networkCoordinator) return;
        UnbindNetworkCoordinator();
        networkCoordinator = found;
        if (networkCoordinator == null) return;
        networkCoordinator.PlayerRegistered += HandlePlayerRegistered;
        networkCoordinator.PlayerLeft += HandlePlayerLeft;
        networkCoordinator.ReadyStateChanged += HandleNetworkReadyChanged;
    }

    private void UnbindNetworkCoordinator()
    {
        if (networkCoordinator == null) return;
        networkCoordinator.PlayerRegistered -= HandlePlayerRegistered;
        networkCoordinator.PlayerLeft -= HandlePlayerLeft;
        networkCoordinator.ReadyStateChanged -= HandleNetworkReadyChanged;
        networkCoordinator = null;
    }

    private void HandlePlayerRegistered(ulong clientId, string displayName)
    {
        if (playerSlots == null || playerSlots.Length == 0) RefreshSlots();
        PlayerSlotView slot = playerSlots.FirstOrDefault(value => value.NetworkClientId == clientId) ??
                              playerSlots.FirstOrDefault(value => !value.IsOccupied);
        if (slot == null) return;
        int slotNumber = Array.IndexOf(playerSlots, slot) + 1;
        NetworkManager manager = NetworkManager.Singleton;
        bool isLocal = manager != null && clientId == manager.LocalClientId;
        bool isHost = manager != null && clientId == NetworkManager.ServerClientId;
        slot.SetPlayer(slotNumber, displayName, "대기 중", isLocal, isHost, false, clientId);
        RefreshStartButton();
    }

    private void HandlePlayerLeft(ulong clientId)
    {
        PlayerSlotView slot = playerSlots?.FirstOrDefault(value => value.NetworkClientId == clientId);
        if (slot == null) return;
        slot.SetEmpty(Array.IndexOf(playerSlots, slot) + 1);
        RefreshStartButton();
    }

    private void HandleNetworkReadyChanged(ulong clientId, bool isReady)
    {
        PlayerSlotView slot = playerSlots?.FirstOrDefault(value => value.NetworkClientId == clientId);
        ApplyNetworkReadyState(slot, isReady);
    }

    private void Unsubscribe()
    {
        if (playerSlots == null) return;
        foreach (PlayerSlotView slot in playerSlots)
        {
            if (slot == null) continue;
            slot.ReadyButtonClicked -= HandleReadyClicked;
            slot.StartGameButtonClicked -= HandleStartClicked;
        }
    }
}
