# 원격 플레이와 배포

목표는 **링크 하나로 브라우저에서, 다른 네트워크에 있는 두 사람이** 플레이하는
것이다. 이 문서는 그 순서를 처음부터 끝까지 적는다.

## 0. 결론부터

| 무엇 | 어디에 |
|---|---|
| 게임 화면 | EC2가 정적 파일로 서빙 (`https://<도메인>/`) |
| 음성 API | 같은 EC2, 같은 도메인의 `/api/*` |
| **경기 연결** | **Unity Relay.** EC2를 지나가지 않는다 |
| 만나는 방법 | **초대코드 6글자** (Relay가 발급) |
| 열어야 하는 포트 | **80, 443, 22 뿐** |

## 1. 왜 Relay인가 — 선택이 아니라 제약이다

브라우저는 **듣는 소켓을 열 수 없다.** UDP가 안 되는 것만이 아니라, TCP로도
서버가 될 수 없다. Unity Transport 소스에 그대로 적혀 있다
(`UnityTransport.cs`):

```csharp
#if UNITY_WEBGL && !UNITY_EDITOR
    if (m_NetworkManager.IsServer && m_ProtocolType != ProtocolType.RelayUnityTransport)
    {
        throw new Exception("WebGL as a server is not supported by Unity Transport, outside the Editor.");
    }
#endif
```

**Relay 프로토콜일 때만 예외다.** 즉 "한 명이 호스트하고 자기 IP를 불러준다"는
기존 모델은 브라우저로 가는 순간 끝난다. 남는 선택지는 두 개였다.

1. EC2에 Unity 전용 서버를 여러 개 띄우고 방 브로커를 만든다.
2. Unity Relay를 쓴다.

**2번을 골랐다** (`DEC-WEBGL-001`). 이유는 세 가지다.

- **초대코드가 공짜로 딸려 온다.** Relay의 join code가 6글자이고, 그게 그대로
  이 게임의 초대코드다. 우리가 코드 테이블을 따로 관리할 이유가 없다.
- **t3.micro(1GB)로는 Unity 헤드리스 서버를 여러 개 못 돌린다.** 인스턴스당
  수백 MB를 먹는다. 정적 파일 + Node 음성 서버까지가 이 사양의 몫이다.
- **리눅스 빌드가 필요 없다.** 이 PC에는 Linux Build Support 모듈이 없고
  (`TASK-DEPLOY-003`), 제출을 앞두고 새 빌드 파이프라인을 검증할 시간이 없다.

Relay를 쓰면 **호스트도 브라우저**다. 밖으로 나가는 연결만 만들기 때문이다.

전송은 어디서나 `wss` 하나로 고정했다. 브라우저는 다른 선택지가 없고, 데스크톱만
`dtls`로 두면 플레이테스트가 검증한 전송과 제출본이 쓰는 전송이 달라진다 — 이
프로젝트가 UDP를 버릴 때 이미 한 번 없앤 분기다.

## 2. 전체 순서

아래 A~G를 순서대로 한다. **A와 B는 사람만 할 수 있는 일**이고 나머지는
스크립트가 있다.

```text
A. Unity Cloud 프로젝트 연결   (Relay가 켜지는 유일한 조건)
B. DuckDNS 도메인 + 보안 그룹
C. EC2 1회 셋업              deploy/setup-ec2.sh
D. 음성 서버 .env
E. WebGL 릴리스 빌드          PawliceAndPurrglar > Build > Build WebGL Release
F. 업로드                     deploy/upload.ps1
G. 2인 실측
```

---

### A. Unity Cloud 프로젝트 연결 — 이걸 안 하면 아무것도 안 된다

지금 `ProjectSettings.asset`의 `cloudProjectId`가 **비어 있다.** 이 상태로
빌드하면 방 만들기가 `Unity 프로젝트 연결이 필요합니다.`로 끝난다
(`RelaySessionService.IsProjectLinked`가 요청을 보내기 전에 잡는다 — 서버까지
갔다가 인증 오류로 돌아오면 네트워크 문제처럼 보이기 때문이다).

1. Unity 에디터를 연다. 우측 상단에서 Unity 계정으로 로그인한다.
2. `Edit > Project Settings > Services`
3. 조직(Organization)을 고르고 **Create project ID** 또는 기존 Unity Cloud
   프로젝트에 **Link**.
4. 브라우저에서 [Unity Cloud 대시보드](https://cloud.unity.com) → 해당 프로젝트
   → **Multiplayer > Relay** → **Get started / Enable**.
5. 돌아와서 확인:

```bash
grep cloudProjectId ProjectSettings/ProjectSettings.asset
```

값이 붙어 있어야 한다. **이 값은 커밋된다** — 두 대의 기계가 같은 프로젝트를
가리켜야 서로의 방에 들어갈 수 있다.

> **요금.** Relay는 무료 티어가 있다. 심사 기간의 2인 데모는 그 안에 든다.
> 대시보드의 사용량 페이지에서 현재 한도를 한 번 확인해 둔다.

에디터에서 바로 확인하는 법: Play를 눌러 로비에서 **방 만들기**. 6글자가 나오면
A는 끝났다.

---

### B. 도메인과 보안 그룹

**https가 필수다.** 두 가지 이유가 겹친다.

- `navigator.mediaDevices`는 **보안 컨텍스트에만 존재한다.** localhost가 아닌
  http에서는 마이크가 아예 열리지 않는다 (`VOICE-011`).
- https 페이지는 `ws://`로 접속할 수 없다. Relay는 `wss`를 주므로 게임 쪽은
  괜찮지만, 우리 음성 API도 https여야 한다.

#### B-1. DuckDNS

1. <https://www.duckdns.org> 에 GitHub/Google 계정으로 로그인.
2. 원하는 이름을 만든다 (예: `pawlice`). 주소는 `pawlice.duckdns.org`가 된다.
3. `current ip` 칸에 **EC2의 퍼블릭 IP**를 넣고 update.
4. 확인:

```bash
nslookup pawlice.duckdns.org
```

> EC2를 재시작하면 퍼블릭 IP가 바뀐다. **탄력적 IP(Elastic IP)를 할당해 붙여
> 두는 편이 낫다** — 심사 도중에 링크가 죽는 것이 가장 나쁘다. 안 붙일 거라면
> 인스턴스 안에서 DuckDNS 갱신 cron을 돌린다.

#### B-2. 보안 그룹

인바운드 규칙을 **딱 세 개**로 만든다.

| 유형 | 포트 | 소스 | 이유 |
|---|---|---|---|
| SSH | 22 | 내 IP | 배포 |
| HTTP | 80 | 0.0.0.0/0 | Let's Encrypt 인증 + https 리다이렉트 |
| HTTPS | 443 | 0.0.0.0/0 | 게임과 API |

**게임 포트(7979)는 열지 않는다.** Relay가 처리하므로 이 기계는 경기 트래픽을
받지 않는다. 예전 문서가 열라고 했던 규칙이 남아 있으면 지운다.

---

### C. EC2 1회 셋업

저장소를 인스턴스로 가져가거나 `deploy/`의 `setup-ec2.sh`·`pawlice.nginx.conf`·`pawlice-voice.service`를 올린 뒤:

```bash
sudo PAWLICE_DOMAIN=pawlice.duckdns.org bash deploy/setup-ec2.sh
```

하는 일: Node 22, `pawlice` 서비스 계정, `/srv/pawlice/{web,server}`,
`/etc/nginx/conf.d/pawlice.conf`, `pawlice-voice.service`, 인증서 갱신 타이머.
여러 번 실행해도 안전하다.

**인증서는 스크립트가 발급하지 않는다.** 이미 있는 것을 읽어 쓰고, 없으면 멈추고
발급 명령을 알려준다:

```bash
sudo certbot --nginx -d pawlice.duckdns.org
```

이렇게 나눈 이유는 Let's Encrypt에 **발급 횟수 제한**이 있어서다. 스크립트가 스스로
받게 만들면 재실행할 때마다 멀쩡한 인증서를 버리고 다시 받는데, 그건 언제 해도 나쁜
거래이고 마감 직전에는 최악이다.

Certbot은 인증서를 발급하고 **갱신 장치는 남기지 않는다.** 90일 뒤 조용히 만료되고,
증상은 "게임이 안 열린다"이며, 배포한 사람이 더 이상 보고 있지 않을 때 일어난다.
스크립트가 하루 두 번 도는 `certbot-renew.timer`를 건다.

B-1(도메인이 이 기계를 가리킴)이 끝나 있어야 발급이 성공한다.

---

### D. 음성 서버 설정

```bash
sudo -u pawlice install -m 600 /dev/null /srv/pawlice/server/.env
sudo -u pawlice nano /srv/pawlice/server/.env    # deploy/voice.env.example 참고
sudo systemctl enable --now pawlice-voice
```

`OPENAI_API_KEY`를 비워 두면 결정적 스텁으로 돈다. 게임은 그대로 플레이되고
동물 명령은 숫자키로 내린다. **모드 600을 지킨다** — 공개 페이지를 서빙하는
기계에 과금되는 키가 얹히는 파일이다.

---

### E. WebGL 릴리스 빌드

> **빌드는 개발 PC에서 한다. EC2에는 산출물만 올린다.**
>
> 소스를 서버에 올려 거기서 빌드하는 방식은 이 인스턴스에서 성립하지 않는다.
> 이유가 넷이고 어느 하나만으로도 충분하다.
>
> 1. **메모리.** IL2CPP와 emscripten 링크가 수 GB를 쓴다. 이 기계는 1.8GB다
> 2. **아키텍처.** 인스턴스가 `aarch64`인데 Unity는 **ARM64 리눅스 에디터를
>    배포하지 않는다**
> 3. **라이선스.** 에디터는 활성화가 필요하다
> 4. **시간.** 이 PC에서 20분인 빌드가 2 vCPU에서는 몇 시간이고, 그동안 같은
>    기계가 게임도 서빙해야 한다
>
> Node 음성 서버도 마찬가지로 **로컬에서 `tsc`를 돌린다.** 서버는 이미 만들어진
> `dist/`를 받고 `npm ci --omit=dev`로 의존성만 설치한다. 1.8GB 기계가 게임을
>서빙하면서 컴파일하는 것이 곧 장애다.

> **`ProjectSettings`를 고쳤으면 반드시 다시 빌드한다.** `cloudProjectId`는
> **빌드에 구워지는 값**이다. 프로젝트를 연결하기 전에 만든 빌드를 올리면, 로비에서
> 방 만들기가 `Unity 프로젝트 연결이 필요합니다.`로 끝난다 — 그런데 **에디터에서는
> 잘 된다.** 에디터는 `ProjectSettings.asset`을 직접 읽기 때문이다.
>
> 2026-08-09에 정확히 이 순서로 당했다: 17:00 빌드 → 00:30 연결 → 그 사이에 배포.
> 증상이 배포본에서만 나타나므로 로비 코드나 Relay를 의심하게 된다. **서버에 있는
> 것은 그 시점의 스냅샷이고, 소스를 고쳐도 저절로 따라가지 않는다.**

Unity 에디터에서:

```text
PawliceAndPurrglar > Build > Build WebGL Release
```

`Builds/Release/WebGL/`로 나온다. 플레이테스트 빌드(`Builds/Playtest/WebGL`)와
폴더가 다르다 — 개발 빌드에는 프로파일러가 붙고 용량이 훨씬 크며, "가장 최근에
만든 WebGL 폴더"가 실수로 올라가는 일을 막기 위해서다.

배치로도 된다:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.5.4f1/Editor/Unity.exe" -batchmode -nographics -quit -projectPath "C:/Users/SSAFY/paws-and-loot" -executeMethod PawsAndLoot.Editor.PlaytestBuild.BuildWebGlRelease -logFile Logs/webgl-release.log
```

빌드 설정 중 배포에 걸리는 것 둘:

- **압축 Gzip + Decompression Fallback 켬.** 켜면 산출물이 `.unityweb`이 되고
  브라우저가 JS로 푼다. **서버에 `Content-Encoding` 헤더를 맞출 필요가 없다.**
  헤더 한 줄 때문에 흰 화면이 뜨는 것이 WebGL 배포에서 가장 흔한 실패이고, 로딩
  1~2초를 주고 그 실패를 사는 편이 낫다.
- **커스텀 템플릿** `Assets/WebGLTemplates/PawliceAndPurrglar`. 기본 템플릿은
  캔버스를 960×600 상자에 가둔다. 이 템플릿은 창을 채우고, 진행률을 보여주고,
  **로딩에 실패하면 그 이유를 화면에 쓴다** — 기본 템플릿은 빈 페이지만 남긴다.

---

### F. 업로드

개발 PC에서, 저장소 루트에서:

```powershell
.\deploy\upload.ps1 -HostName ec2-user@pawlice.duckdns.org -KeyPath $HOME\Downloads\paws-and-loot-key.pem
```

스테이징 폴더에 올린 뒤 한 번에 자리를 바꾼다. 절반만 올라간 빌드를 심사위원이
여는 일이 없다. 직전 빌드는 `/srv/pawlice/web.previous`에 남고, 그게 롤백이다.

게임만 다시 올릴 때는 `-GameOnly`.

확인:

```bash
curl -I https://pawlice.duckdns.org/          # 200
curl    https://pawlice.duckdns.org/health    # {"status":"ok"}
```

---

### G. 2인 실측 — 여기까지 해야 끝이다

**서로 다른 네트워크에서** 두 사람이 연다. 한 사람은 휴대폰 테더링이면 충분하다.
같은 공유기에서 하면 이 변경이 고치려던 것을 시험하지 않는다.

1. 둘 다 `https://pawlice.duckdns.org/` 를 연다.
2. A가 **방 만들기** → 6글자가 뜬다 → **코드 복사**.
3. 코드를 상대에게 보낸다.
4. B가 코드칸에 붙여넣고 **방 입장**.
5. 둘 다 "상대 플레이어와 연결되었습니다"가 뜨면 A가 **게임 시작**.

확인할 것:

- [ ] 두 캐릭터가 서로의 화면에서 움직인다
- [ ] 보물 획득·판매·체포가 양쪽에 반영된다
- [ ] 결과 화면의 승자가 양쪽에서 같다
- [ ] 마이크 권한 팝업이 뜬다 (https가 제대로 걸렸다는 증거)
- [ ] 재경기가 양쪽을 로비로 되돌린다

## 3. 두 프로세스 회귀 (개발 PC)

Windows 빌드 두 개로 도는 기존 시나리오는 그대로다. 초대코드 경로를 시험하려면
`-netJoinMode ui`를 쓴다. **호스트가 받은 코드를 파일에 적고 클라이언트가 그걸
읽는다** (`%USERPROFILE%\AppData\LocalLow\...\net-invite-code.txt`) — 코드는
호스트가 요청하기 전에는 존재하지 않으므로 런처가 양쪽에 미리 넘겨줄 수 없다.

```bash
"Builds/Playtest/Windows/PawsAndLoot.exe" -netLobby host   -netJoinMode ui -netScenario full -netMatchSeconds 60
"Builds/Playtest/Windows/PawsAndLoot.exe" -netLobby client -netJoinMode ui -netScenario full -netMatchSeconds 60
```

`-netJoinMode api`는 여전히 **직접 IP**를 쓴다. 인터넷도 Unity 계정도 없이
도는 유일한 경로이므로 남겨 뒀다. 로비 UI나 Relay를 건드렸으면 `ui`로 한 번은
돌린다.

`-netInviteCode ABC123`으로 코드를 직접 넘길 수도 있다.

## 4. 되돌리는 법

`NetworkSessionController`에 직접 IP 경로(`TryStartHost` / `TryJoin`)가 그대로
있다. 로비에서만 사라졌다. WebGL을 포기하고 데스크톱 배포로 돌아간다면 로비에
주소·포트 칸을 되살리면 되고, 그 아래 계층은 손댈 것이 없다.

Relay만 빼고 싶다면 `Packages/manifest.json`에서 세 패키지를 지우면 된다.
`PAWS_RELAY` 심볼이 사라지고 `RelaySessionService`가 "패키지가 없다"고 답한다 —
컴파일은 된다.

## 5. 남은 것과 안 하는 것

| 항목 | 상태 |
|---|---|
| `TASK-DEPLOY-001` 전송 WebSocket | DONE (2026-08-05) |
| `TASK-DEPLOY-002` 전용 서버 모드 | DONE (2026-08-05). Relay 채택으로 **미사용** |
| `TASK-DEPLOY-003` 리눅스 헤드리스 빌드 | **불필요해짐.** Relay가 대신한다 |
| `TASK-DEPLOY-004` TLS 종료 | nginx + Certbot + DuckDNS. 위 B·C |
| `TASK-DEPLOY-005` 자기 도메인 자동 접속 | DONE. `VoiceBackendAddress`가 페이지 오리진을 쓴다 |
| `TASK-DEPLOY-006` 보안 그룹 | 위 B-2. 게임 포트를 열지 않는다 |

**LAN 방 목록은 브라우저에서 꺼진다.** UDP 브로드캐스트라 WebGL에 소켓이 없고,
`UdpClient`는 컴파일이 아니라 첫 호출에서 터지므로 통째로 컴파일에서 뺐다.
데스크톱 빌드에서 `TryStartHost`로 연 방에만 남아 있다 — Relay 방은 이 네트워크에
주소가 없으므로 광고하지 않는다. 광고하면 아무것도 듣고 있지 않은 포트를 가리키는
방이 목록에 뜬다.
