# LiarPainter - Unity Netcode for GameObjects 시작 가이드

이 문서는 Unity 공식 **Netcode for GameObjects 2.13 Client-Server Quickstart**를 바탕으로, LiarPainter 프로젝트에 멀티플레이 기초 구조를 적용하기 전에 알아야 할 내용과 작업 순서를 정리한 문서입니다.

원문: [Client-server quickstart for Netcode for GameObjects 2.13](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/tutorials/get-started-with-ngo.html)

> 현재 상태: 이 프로젝트는 **Unity 6000.3.18f1**과 **Netcode for GameObjects 2.13.3**을 사용합니다. Multiplayer Services 기반의 비공개/공개 세션 생성, 참가 코드 입장, Relay 연결, 네트워크 씬 전환, 준비·라운드 상태·투표·그림 선 동기화가 구현되어 있습니다.

## 현재 프로젝트에서 실행하는 방법

### Host

1. `Assets/Scenes/MainScene.unity`를 열고 Play Mode를 시작합니다.
2. 닉네임을 입력한 뒤 방 찾기 화면으로 이동합니다.
3. **방 만들기**에서 방 이름, 최대 인원, 공개 여부를 정하고 생성합니다.
4. GameScene 상단의 **참가 코드**를 다른 플레이어에게 전달합니다. **코드 복사** 버튼으로 클립보드에 복사할 수 있습니다.

### Client

1. 별도 빌드 또는 Multiplayer Play Mode 가상 플레이어에서 MainScene을 실행합니다.
2. 방 찾기 화면의 검색 칸에 Host의 참가 코드를 입력합니다.
3. **빠른 참가**를 누릅니다.
4. 연결이 완료되면 Host가 열어 둔 GameScene으로 자동 이동합니다.

### 현재 동기화되는 항목

- 플레이어 접속·퇴장과 닉네임
- 준비 상태와 Host의 게임 시작 요청
- 역할 및 개인 키워드 전달
- 질문, 현재 단계, 제한 시간, 익명 그림 차례
- 현재 차례 플레이어의 그림 선 생성과 지우기
- 투표와 결과
- Host 종료 또는 연결 해제 시 MainScene 복귀

온라인 세션 기능을 사용하려면 Unity Dashboard에 연결된 프로젝트와 인터넷 연결이 필요합니다. 로컬 반복 테스트는 **Liar Painter > Multiplayer Test**에서 Multiplayer Play Mode 시나리오 창을 열어 진행할 수 있습니다.

## 1. 이 튜토리얼에서 배우는 것

- `NetworkManager`로 Host, Client, Server를 시작하는 방법
- 접속한 플레이어마다 Player Prefab을 자동 생성하는 방법
- `NetworkObject`와 `NetworkBehaviour`의 역할
- RPC로 클라이언트와 서버 사이에 요청을 전달하는 방법
- `NetworkVariable`로 상태를 동기화하는 방법
- `NetworkTransform`으로 위치, 회전, 크기를 동기화하는 방법
- 빌드 또는 Multiplayer Play Mode로 여러 실행 환경을 테스트하는 방법

이 튜토리얼은 인터넷 방 생성이나 매치메이킹까지 구현하는 문서가 아닙니다. 먼저 한 컴퓨터 또는 같은 네트워크 환경에서 NGO의 기본적인 서버-클라이언트 구조를 검증하는 입문 단계입니다.

## 2. 핵심 용어

| 용어 | 의미 |
| --- | --- |
| Host | 서버와 클라이언트를 한 프로세스에서 동시에 실행합니다. Host 자신도 플레이어로 참가합니다. |
| Client | 서버에 접속해 플레이하는 참가자입니다. |
| Server | 게임 상태를 관리하는 서버만 실행합니다. 전용 서버 모드에서는 로컬 플레이어를 만들지 않습니다. |
| `NetworkManager` | 접속 시작/종료, Transport, Player Prefab, 네트워크 프리팹과 씬 동기화를 관리하는 중심 컴포넌트입니다. |
| `UnityTransport` | NGO가 네트워크 데이터를 실제로 전송할 때 사용하는 기본 Transport입니다. |
| `NetworkObject` | NGO가 네트워크상에서 식별하고 생성·소유·제거할 수 있게 만드는 컴포넌트입니다. |
| `NetworkBehaviour` | RPC, `NetworkVariable`, 소유권 상태 등을 사용할 수 있는 네트워크용 `MonoBehaviour`입니다. |
| Player Object | 특정 클라이언트를 대표하며 해당 클라이언트가 소유하는 `NetworkObject`입니다. |
| RPC | 한 실행 환경에서 다른 실행 환경의 메서드 실행을 요청하는 기능입니다. |
| `NetworkVariable` | 서버가 관리하는 값을 현재 접속자와 나중에 접속한 클라이언트에 동기화하는 변수입니다. |
| `NetworkTransform` | Transform의 위치, 회전, 크기를 네트워크로 동기화하는 컴포넌트입니다. |

## 3. 전체 학습 흐름

```text
NGO 설치
  -> NetworkManager + UnityTransport 생성
  -> NetworkObject가 있는 Player Prefab 등록
  -> 플레이할 씬을 빌드 목록에 추가
  -> Host 실행으로 기본 연결 확인
  -> 실행 중 Host/Client/Server를 선택하는 임시 UI 추가
  -> RPC 송수신 확인
  -> NetworkVariable로 위치 상태 동기화
  -> NetworkTransform으로 이동 동기화 확인
  -> 두 개 이상의 실행 환경에서 테스트
```

## 4. 사전 준비

공식 문서의 요구 사항은 다음과 같습니다.

- 유효한 라이선스가 있는 Unity 계정
- Unity Hub
- Unity 2022.3 이상
- `Assets/Scripts` 폴더

LiarPainter는 Unity 6 프로젝트이므로 에디터 버전 조건은 충족합니다.

### NGO 패키지 설치

1. Unity 메뉴에서 **Window > Package Manager**를 엽니다.
2. 패키지 목록을 **Unity Registry**로 변경합니다.
3. **Netcode for GameObjects**를 검색하고 설치합니다.
4. 설치 후 Console에 컴파일 오류가 없는지 확인합니다.

튜토리얼과 동일하게 진행하려면 NGO 2.13 계열을 기준으로 합니다. 실제 설치 가능한 버전과 Unity 6.3의 호환성은 Package Manager에서 확인해야 합니다.

## 5. 기본 네트워크 구성

### 5.1 NetworkManager 만들기

1. Hierarchy에서 빈 GameObject를 만듭니다.
2. 이름을 `NetworkManager`로 지정합니다.
3. **Add Component > Netcode > NetworkManager**를 추가합니다.
4. Transport/Protocol에 `UnityTransport`를 지정합니다.
5. 씬을 저장합니다.

`NetworkManager`는 한 씬에 하나만 두는 것이 기본입니다.

### 5.2 Player Prefab 만들기

공식 예제는 3D Capsule을 사용하지만, LiarPainter가 2D 게임이라면 Sprite 기반 GameObject로 바꿔도 됩니다. 중요한 것은 외형이 아니라 네트워크 컴포넌트 구성입니다.

1. 임시 Player GameObject를 생성하고 이름을 `Player`로 지정합니다.
2. Player의 루트에 `NetworkObject`를 추가합니다.
3. `Assets/Prefabs` 폴더를 만듭니다.
4. Player를 `Assets/Prefabs`로 드래그해 Prefab으로 만듭니다.
5. 씬에 있던 원본 Player는 삭제합니다.
6. `NetworkManager`의 **Player Prefab** 필드에 Player Prefab을 지정합니다.
7. 씬을 저장합니다.

Player Prefab을 지정하면 클라이언트가 접속할 때 NGO가 해당 클라이언트의 Player Object를 자동으로 생성합니다. Player Prefab이 비어 있으면 접속해도 플레이어 오브젝트가 생성되지 않습니다.

> Player Object는 씬에 미리 배치하는 오브젝트가 아니라, 등록된 네트워크 Prefab으로 만들어야 합니다.

### 5.3 씬 등록

`NetworkManager`의 **Enable Scene Management**는 기본적으로 활성화되어 있습니다. 동기화할 씬은 반드시 빌드 씬 목록에 등록해야 합니다.

Unity 버전에 따라 다음 메뉴 중 하나에서 현재 씬을 추가합니다.

- **File > Build Profiles**
- 이전 버전: **File > Build Settings > Add Open Scenes**

성공 기준은 현재 게임 씬이 빌드할 씬 목록에 표시되는 것입니다.

## 6. 첫 Host 실행 테스트

1. Play Mode에 들어갑니다.
2. Hierarchy에서 `NetworkManager`를 선택합니다.
3. Inspector의 **Start Host**를 누릅니다.
4. 버튼이 **Stop Host**로 바뀌는지 확인합니다.
5. Player Prefab 인스턴스가 Hierarchy에 생성되는지 확인합니다.

Host는 서버와 클라이언트를 동시에 실행합니다. 따라서 Host 하나만 실행해도 서버 연결과 로컬 플레이어 생성을 먼저 확인할 수 있습니다.

## 7. 실행 중 연결 모드를 선택하는 UI

공식 예제의 `HelloWorldManager.cs`는 개발용 IMGUI 버튼을 제공하며 다음 API를 호출합니다.

```csharp
networkManager.StartHost();
networkManager.StartClient();
networkManager.StartServer();
```

이 스크립트는 `NetworkManager`와 같은 GameObject에 추가합니다. 실행 전에는 Host, Client, Server 버튼을 보여 주고, 연결 후에는 현재 Transport와 실행 모드를 표시합니다.

각 모드의 차이는 다음과 같습니다.

- **Host**: 서버를 열고 자신도 클라이언트로 참가합니다.
- **Client**: 이미 실행 중인 서버에 참가합니다.
- **Server**: 서버만 실행하며 로컬 Player Object를 만들지 않습니다.

이 UI는 학습 및 연결 테스트용입니다. 최종 게임에서는 실제 로비/메뉴 UI로 교체합니다.

## 8. RPC 이해하기

RPC는 한쪽에서 다른 쪽의 로직 실행을 요청할 때 사용합니다.

```csharp
[Rpc(SendTo.Server)]
private void SubmitRequestRpc()
{
    // 서버에서 실행
}

[Rpc(SendTo.ClientsAndHost)]
private void NotifyClientsRpc()
{
    // 모든 클라이언트와 Host의 클라이언트 측에서 실행
}
```

공식 `RpcTest.cs` 예제의 흐름은 다음과 같습니다.

1. Player Object가 네트워크에 Spawn됩니다.
2. 서버가 아니면서 해당 Player Object의 소유자인 Client만 첫 RPC를 서버로 보냅니다.
3. 서버가 요청을 받고 모든 Client와 Host로 RPC를 보냅니다.
4. Player Object의 소유자가 다시 서버로 다음 번호를 보냅니다.
5. Console에서 서버와 클라이언트의 송수신 로그를 확인합니다.

`IsOwner` 검사가 중요한 이유는 모든 클라이언트에 동일한 Player Prefab과 스크립트가 존재하기 때문입니다. 소유권 검사가 없으면 다른 플레이어의 오브젝트에서도 입력이나 요청을 실행할 수 있습니다.

> 공식 `RpcTest`는 RPC가 계속 왕복하는 학습용 예제입니다. 통신 검증 후에는 제거해야 하며 실제 게임 로직으로 그대로 사용하면 불필요한 네트워크 트래픽과 로그가 계속 발생합니다.

## 9. NetworkVariable로 위치 동기화하기

공식 `HelloWorldPlayer.cs`는 서버가 플레이어 위치의 최종 값을 정하는 구조를 보여 줍니다.

```csharp
public class HelloWorldPlayer : NetworkBehaviour
{
    public NetworkVariable<Vector3> Position = new();

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            RequestMove();
        }
    }

    public void RequestMove()
    {
        SubmitPositionRequestRpc();
    }

    [Rpc(SendTo.Server)]
    private void SubmitPositionRequestRpc()
    {
        Vector3 newPosition = GetRandomPosition();
        Position.Value = newPosition;
    }

    private void Update()
    {
        transform.position = Position.Value;
    }
}
```

핵심 흐름은 다음과 같습니다.

```text
소유 Client가 이동 요청
  -> Server RPC 실행
  -> Server가 Position.Value 변경
  -> NetworkVariable 값이 Client들에 복제
  -> 각 실행 환경이 같은 위치를 적용
```

중요한 규칙:

- `NetworkVariable`은 `NetworkBehaviour` 안에 선언합니다.
- 해당 GameObject 또는 부모에는 `NetworkObject`가 있어야 합니다.
- 기본 권한에서는 서버만 값을 변경합니다.
- `OnNetworkSpawn()`은 네트워크 Spawn이 완료된 뒤 호출됩니다.
- 로컬 입력은 보통 `IsOwner` 또는 `IsLocalPlayer`로 제한합니다.
- 중요한 게임 상태는 클라이언트 값을 그대로 신뢰하지 말고 서버에서 검증합니다.

## 10. NetworkTransform으로 Transform 동기화하기

`NetworkTransform`은 위치, 회전, 크기 변화를 자동으로 전송합니다.

1. Player Prefab을 선택합니다.
2. **Add Component > Netcode > NetworkTransform**을 추가합니다.
3. 서버에서만 Player의 Transform을 변경하는 테스트 스크립트를 추가합니다.
4. Host와 Client 양쪽에서 같은 움직임이 보이는지 확인합니다.

공식 예제는 서버에서 원형 궤도를 계산합니다.

```csharp
private void Update()
{
    if (!IsServer)
    {
        return;
    }

    float theta = Time.frameCount / 10f;
    transform.position = new Vector3(Mathf.Cos(theta), 0f, Mathf.Sin(theta));
}
```

서버만 Transform을 변경하고 `NetworkTransform`이 그 결과를 클라이언트에 복제합니다. `NetworkVariable<Vector3>`와 `NetworkTransform`은 둘 다 위치 동기화를 설명하기 위한 별도 학습 예제이므로, 실제 이동 시스템에서는 책임이 중복되지 않게 하나의 명확한 동기화 방식을 설계해야 합니다.

## 11. 여러 실행 환경에서 테스트하기

Host 하나만으로는 실제 Client 접속을 완전히 확인할 수 없습니다. 최소 두 실행 환경을 사용합니다.

### 방법 A: Editor + 빌드

1. 현재 씬을 빌드 목록에 추가합니다.
2. 실행 파일을 빌드합니다.
3. Editor에서 Host를 시작합니다.
4. 빌드에서 Client를 시작합니다.

### 방법 B: 빌드 두 개

서로 다른 폴더에 빌드한 두 실행 파일을 실행하고 하나는 Host, 하나는 Client로 시작합니다.

### 방법 C: Multiplayer Play Mode

Multiplayer Play Mode 패키지를 사용해 여러 Editor 인스턴스로 테스트합니다. 반복 테스트에는 이 방법이 편리합니다.

## 12. 단계별 완료 체크리스트

### 기본 설정

- [ ] Netcode for GameObjects가 설치되어 있다.
- [ ] `NetworkManager`에 `UnityTransport`가 지정되어 있다.
- [ ] Player Prefab 루트에 `NetworkObject`가 있다.
- [ ] Player Prefab이 `NetworkManager`의 Player Prefab에 지정되어 있다.
- [ ] 게임 씬이 빌드 씬 목록에 등록되어 있다.

### 단일 Host 테스트

- [ ] Play Mode에서 Host가 시작된다.
- [ ] 접속 후 Player Object가 한 개 생성된다.
- [ ] Console에 NGO 관련 오류가 없다.

### Host + Client 테스트

- [ ] Client가 Host에 접속한다.
- [ ] 접속자마다 Player Object가 하나씩 생성된다.
- [ ] 각 Client는 자기 Player만 조작한다.
- [ ] RPC가 예상한 실행 대상에서만 호출된다.
- [ ] 위치 변경이 모든 실행 환경에서 동일하게 보인다.
- [ ] 늦게 접속한 Client도 현재 상태를 전달받는다.

## 13. 자주 발생하는 문제

### 접속했는데 Player가 생성되지 않음

- `NetworkManager`의 Player Prefab이 비어 있는지 확인합니다.
- Player Prefab 루트에 `NetworkObject`가 있는지 확인합니다.
- Player를 씬에 직접 배치해 둔 것은 아닌지 확인합니다.

### Host는 되지만 Client가 접속하지 못함

- Host가 먼저 실행되었는지 확인합니다.
- `UnityTransport`의 주소와 포트가 Host와 Client에서 같은지 확인합니다.
- 로컬 방화벽이 연결을 막고 있지 않은지 확인합니다.
- 서로 다른 컴퓨터라면 `127.0.0.1`이 아니라 Host 컴퓨터의 실제 주소가 필요합니다.

### 움직임이 다른 실행 환경에 보이지 않음

- Player Prefab에 `NetworkTransform`이 있는지 확인합니다.
- 또는 서버가 `NetworkVariable` 값을 실제로 변경하는지 확인합니다.
- 클라이언트가 권한 없이 서버 전용 상태를 직접 변경하고 있지 않은지 확인합니다.
- 이동 코드를 모든 Player 인스턴스에 실행하지 않고 소유권으로 제한했는지 확인합니다.

### 씬 동기화 오류

- 씬이 빌드 씬 목록에 등록되어 있는지 확인합니다.
- `Enable Scene Management` 설정을 확인합니다.
- Host와 Client가 같은 프로젝트/콘텐츠 버전을 실행하는지 확인합니다.

## 14. LiarPainter에 적용할 때의 다음 단계

공식 예제의 Capsule과 랜덤 이동은 NGO 개념 확인용입니다. LiarPainter에서는 다음 순서로 바꾸는 것이 좋습니다.

1. NGO 패키지 설치 및 Host/Client 연결 검증
2. LiarPainter용 Player Prefab을 네트워크 Player Object로 구성
3. 로컬 플레이어만 입력과 카메라를 사용하도록 소유권 분리
4. 플레이어 이동을 서버 권한 또는 소유자 권한 중 한 방식으로 확정
5. 그림 데이터, 정답 제출, 점수, 라운드 시간을 서버 권한 상태로 설계
6. 대기실 → 게임 시작 → 라운드 진행 → 결과 화면의 네트워크 상태 흐름 구현
7. 연결 종료, 재접속, Host 종료에 대한 처리 추가
8. 마지막에 실제 인터넷 접속 방식과 세션 참가 흐름 추가

튜토리얼을 완료했다는 기준은 단순히 Client가 접속하는 것이 아니라, **두 실행 환경에서 플레이어 생성·소유권·RPC·상태 동기화가 의도대로 동작하는 것**입니다.

## 참고 자료

- [Netcode for GameObjects 2.13 Client-Server Quickstart](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/tutorials/get-started-with-ngo.html)
- [Netcode for GameObjects - NetworkVariable](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/basics/networkvariable.html)
- [Netcode for GameObjects - NetworkObject](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.13/manual/components/core/networkobject.html)
