using UnityEngine;
using Unity.Services.Multiplayer;

// The prefab's JoinButton calls Join through its Inspector On Click event.
public sealed class RoomListItemAction : MonoBehaviour
{
    private RoomNetworkUIController owner;
    private ISessionInfo room;
    public void Configure(RoomNetworkUIController controller, ISessionInfo info)
    {
        owner = controller;
        room = info;
    }
    public void Join()
    {
        if (owner != null && room != null) owner.SelectRoom(room);
    }
}
