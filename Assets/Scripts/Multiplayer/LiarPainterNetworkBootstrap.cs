using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public static class LiarPainterNetworkBootstrap
{
    // AfterSceneLoad is intentional: MainScene already contains the authored
    // NetworkManager and its NetworkPrefabsList. Configure that instance instead
    // of creating a competing manager before the scene has loaded.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureNetworkRoot()
    {
        NetworkManager manager = NetworkManager.Singleton;
        GameObject root;

        if (manager == null)
        {
            root = new GameObject("LiarPainterNetworkRoot");
            manager = root.AddComponent<NetworkManager>();
        }
        else
        {
            root = manager.gameObject;
        }

        Object.DontDestroyOnLoad(root);

        UnityTransport transport = root.GetComponent<UnityTransport>();
        if (transport == null) transport = root.AddComponent<UnityTransport>();
        manager.NetworkConfig.NetworkTransport = transport;
        manager.NetworkConfig.EnableSceneManagement = true;

        if (root.GetComponent<LiarPainterSessionManager>() == null)
            root.AddComponent<LiarPainterSessionManager>();
    }
}
