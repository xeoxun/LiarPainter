using UnityEngine;

public sealed class DrawingStrokeOwner : MonoBehaviour
{
    public int StrokeId { get; private set; }
    public string OwnerPlayerId { get; private set; }

    public void Initialize(int strokeId, string ownerPlayerId)
    {
        StrokeId = strokeId;
        OwnerPlayerId = ownerPlayerId;
    }
}
