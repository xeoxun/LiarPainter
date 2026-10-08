using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class LiarPainterSessionManager : MonoBehaviour
{
    private const string GameTypeProperty = "gameType";
    private const string GameTypeValue = "LiarPainter";
    private const string PinPrefix = "LiarPainter:";
    public static LiarPainterSessionManager Instance { get; private set; }

    [SerializeField] private int maximumPlayers = 8;
    [SerializeField] private string lobbySceneName = "MainScene";

    public ISession CurrentSession { get; private set; }
    public bool IsHost => CurrentSession is IHostSession;
    public string JoinCode => CurrentSession?.Code ?? string.Empty;
    public int RoomCapacity => CurrentSession?.MaxPlayers ?? maximumPlayers;
    public bool IsBusy { get; private set; }

    public event Action<ISession> SessionJoined;
    public event Action<string> SessionClosed;
    public event Action<string> SessionError;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        BindNetworkCallbacks();
    }

    private void OnDisable()
    {
        UnbindNetworkCallbacks();
    }

    public async Task<bool> CreateRoomAsync(string roomName, int maxPlayers, bool passwordProtected = false,
        string fourDigitPin = "")
    {
        if (IsBusy || CurrentSession != null) return false;
        if (passwordProtected && !IsValidFourDigitPin(fourDigitPin))
        {
            SessionError?.Invoke("비밀번호는 숫자 4자리로 입력하세요.");
            return false;
        }

        IsBusy = true;
        try
        {
            await EnsureServicesReadyAsync();
            SessionOptions options = new SessionOptions
            {
                Name = string.IsNullOrWhiteSpace(roomName) ? "Liar Painter Room" : roomName.Trim(),
                // 세션 서비스가 이 값을 기준으로 초과 입장을 거절합니다.
                MaxPlayers = Mathf.Clamp(maxPlayers, 4, maximumPlayers),
                // 목록에 노출되는 비밀번호 보호 방이므로 UGS의 비공개 플래그는 사용하지 않습니다.
                IsPrivate = false,
                Password = passwordProtected ? BuildServicePassword(fourDigitPin) : null,
                SessionProperties = new Dictionary<string, SessionProperty>
                {
                    [GameTypeProperty] = new SessionProperty(
                        GameTypeValue,
                        VisibilityPropertyOptions.Public,
                        PropertyIndex.String1)
                }
            }.WithRelayNetwork();

            CurrentSession = await MultiplayerService.Instance.CreateSessionAsync(options);
            SubscribeSession(CurrentSession);
            BindNetworkCallbacks();
            SessionJoined?.Invoke(CurrentSession);
            return true;
        }
        catch (Exception exception)
        {
            ReportError(exception);
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> JoinRoomByCodeAsync(string joinCode, string fourDigitPin = "")
    {
        if (IsBusy || CurrentSession != null || string.IsNullOrWhiteSpace(joinCode)) return false;
        IsBusy = true;
        try
        {
            await EnsureServicesReadyAsync();
            JoinSessionOptions options = string.IsNullOrEmpty(fourDigitPin)
                ? null
                : new JoinSessionOptions { Password = BuildServicePassword(fourDigitPin) };
            CurrentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode.Trim(), options);
            SubscribeSession(CurrentSession);
            BindNetworkCallbacks();
            SessionJoined?.Invoke(CurrentSession);
            return true;
        }
        catch (Exception exception)
        {
            ReportError(exception);
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> JoinRoomByIdAsync(string sessionId, string fourDigitPin = "")
    {
        if (IsBusy || CurrentSession != null || string.IsNullOrWhiteSpace(sessionId)) return false;
        if (!string.IsNullOrEmpty(fourDigitPin) && !IsValidFourDigitPin(fourDigitPin))
        {
            SessionError?.Invoke("비밀번호는 숫자 4자리로 입력하세요.");
            return false;
        }

        IsBusy = true;
        try
        {
            await EnsureServicesReadyAsync();
            JoinSessionOptions options = string.IsNullOrEmpty(fourDigitPin)
                ? null
                : new JoinSessionOptions { Password = BuildServicePassword(fourDigitPin) };
            CurrentSession = await MultiplayerService.Instance.JoinSessionByIdAsync(sessionId.Trim(), options);
            SubscribeSession(CurrentSession);
            BindNetworkCallbacks();
            SessionJoined?.Invoke(CurrentSession);
            return true;
        }
        catch (Exception exception)
        {
            CurrentSession = null;
            ReportError(exception);
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<IList<ISessionInfo>> QueryRoomsAsync()
    {
        try
        {
            await EnsureServicesReadyAsync();
            QuerySessionsResults results = await MultiplayerService.Instance.QuerySessionsAsync(
                new QuerySessionsOptions
                {
                    Count = 50,
                    FilterOptions = new List<FilterOption>
                    {
                        new FilterOption(FilterField.StringIndex1, GameTypeValue, FilterOperation.Equal),
                        new FilterOption(FilterField.AvailableSlots, "0", FilterOperation.Greater),
                        new FilterOption(FilterField.IsLocked, "false", FilterOperation.Equal)
                    }
                });
            return results.Sessions;
        }
        catch (Exception exception)
        {
            ReportError(exception);
            return Array.Empty<ISessionInfo>();
        }
    }

    public static bool IsValidFourDigitPin(string pin)
    {
        if (pin == null || pin.Length != 4) return false;
        for (int i = 0; i < pin.Length; i++)
            if (pin[i] < '0' || pin[i] > '9') return false;
        return true;
    }

    private static string BuildServicePassword(string pin) => PinPrefix + pin;

    public async Task LeaveRoomAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        bool wasHost = IsHost;
        try
        {
            ISession session = CurrentSession;
            CurrentSession = null;
            if (session != null)
            {
                UnsubscribeSession(session);
                if (session is IHostSession hostSession)
                    await hostSession.DeleteAsync();
                else
                    await session.LeaveAsync();
            }
        }
        catch (Exception exception)
        {
            ReportError(exception);
        }
        finally
        {
            IsBusy = false;
            ReturnToLobby(wasHost ? "호스트가 방을 종료했습니다." : "방에서 나갔습니다.");
        }
    }

    private static async Task EnsureServicesReadyAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            InitializationOptions options = new InitializationOptions();
            options.SetProfile(GetLocalTestProfile());
            await UnityServices.InitializeAsync(options);
        }
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    private static string GetLocalTestProfile()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int i = 0; i < arguments.Length - 1; i++)
        {
            if (string.Equals(arguments[i], "-liarProfile", StringComparison.OrdinalIgnoreCase))
                return SanitizeProfile(arguments[i + 1]);
        }

        // 같은 Windows 빌드를 여러 개 실행해도 각 프로세스가 서로 다른 익명 플레이어가 됩니다.
        return Application.isEditor
            ? "editor"
            : $"local_{System.Diagnostics.Process.GetCurrentProcess().Id}";
    }

    private static string SanitizeProfile(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "local_player";
        char[] characters = value.Trim().ToCharArray();
        for (int i = 0; i < characters.Length; i++)
        {
            char character = characters[i];
            if (!char.IsLetterOrDigit(character) && character != '-' && character != '_')
                characters[i] = '_';
        }
        return new string(characters, 0, Mathf.Min(characters.Length, 30));
    }

    private void BindNetworkCallbacks()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null) return;
        manager.OnClientDisconnectCallback -= HandleClientDisconnected;
        manager.OnClientDisconnectCallback += HandleClientDisconnected;
    }

    private void UnbindNetworkCallbacks()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager != null) manager.OnClientDisconnectCallback -= HandleClientDisconnected;
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || manager.IsServer || clientId != manager.LocalClientId) return;

        ISession session = CurrentSession;
        CurrentSession = null;
        if (session != null) UnsubscribeSession(session);
        ReturnToLobby("호스트 연결이 끊겨 방이 종료되었습니다.");
    }

    private void SubscribeSession(ISession session)
    {
        if (session != null) session.RemovedFromSession += HandleRemovedFromSession;
    }

    private void UnsubscribeSession(ISession session)
    {
        if (session != null) session.RemovedFromSession -= HandleRemovedFromSession;
    }

    private void HandleRemovedFromSession()
    {
        if (CurrentSession == null) return;
        UnsubscribeSession(CurrentSession);
        CurrentSession = null;
        ReturnToLobby("방이 종료되었습니다.");
    }

    private void ReturnToLobby(string reason)
    {
        SessionClosed?.Invoke(reason);
        PlayerPrefs.SetString("LiarPainter.LastDisconnectReason", reason);
        if (!string.IsNullOrWhiteSpace(lobbySceneName) && SceneManager.GetActiveScene().name != lobbySceneName)
            SceneManager.LoadScene(lobbySceneName);
    }

    private void ReportError(Exception exception)
    {
        Debug.LogException(exception);
        SessionError?.Invoke(exception.Message);
    }
}
