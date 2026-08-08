# 자산 출처와 라이선스 (Asset License Record)

이 저장소가 담고 있거나 빌드에 포함되는 모든 자산의 출처를 적는다. 제출물과 함께
배포되는 파일이며, 새 자산을 추가할 때 **추가하는 변경에서 같이 갱신한다.**

효과음의 파일별 상세는 [docs/17_AUDIO_CREDITS.md](docs/17_AUDIO_CREDITS.md)에 있다.
이 문서는 그것을 요약하고, 모델·폰트·엔진·패키지까지 함께 다룬다.

## 요약

| 자산 | 출처 | 권리 | 상업적 사용 |
|---|---|---|---|
| 3D 모델 41개 | 팀 자체 원화 → Tripo AI 생성 → Blender/Unity 수정 | **팀 소유** | 가능 |
| 효과음 10개 | freesound.org | CC0 (공유 저작물) | 가능, 표기 의무 없음 |
| 폰트 | Unity 내장 + **한글 OFL 폰트 예정** | Unity 라이선스 / OFL 1.1 | 가능 |
| 엔진·패키지 | Unity 6000.5.4f1 + Unity 공식 패키지 | Unity 라이선스 | 가능 |
| 배경음악 | Suno AI (예정) | 유료 구독 생성분 | 가능 (조건부) |
| 음성 인식·분류 | OpenAI API (런타임 호출) | 자산 아님, 서비스 | 가능 |

## 1. 3D 모델

### 대상

`Assets/_Project/Art/` 아래 전부. 총 41개.

| 폴더 | 개수 | 내용 |
|---|---:|---|
| `Props/` | 20 | 보물, 던지는 물건, 상호작용 소품 |
| `Environment/` | 10 | 도로, 잔디, 분수, 벽 |
| `Buildings/` | 5 | 경찰서, 슈퍼마켓, 서점, 주택 2종 |
| `Characters/` | 5 | 경찰, 도둑, 동물 |
| `TechnicalValidation/` | 1 | 리그 임포트 검증용 |

작업 원본은 `ArtSource/Blender/`에 있다. Unity가 쓰지 않는 원본이며 같은 출처다.

### 제작 경위

1. **원화(2D)를 팀이 직접 제작**했다.
2. 그 원화를 입력으로 **Tripo AI**에서 3D 모델을 생성했다.
3. 생성은 **Tripo Pro 구독** 기간에 이루어졌고, 해당 요금제는 **상업적 사용 권리**를
   포함한다.
4. 생성된 모델을 **Blender와 Unity에서 수정·최적화**했다.

### 권리

```text
© 2026 PawliceAndPurrglar 팀. All rights reserved.
```

> **팀명 또는 대표 제작자명을 실제 표기로 바꿔 넣을 것.** 위 이름은 Unity 프로젝트
> 설정의 `companyName`을 그대로 쓴 임시값이다. 공모전 제출 서류의 팀명과 일치해야
> 한다.

| 항목 | 내용 |
|---|---|
| 원화 저작권 | 팀 (내부 제작) |
| 3D 생성 도구 | Tripo AI |
| 생성 시점 요금제 | Pro |
| 허용 범위 | 게임 내 상업적·비상업적 사용 |
| 추가 편집 | Blender, Unity |
| **원본 FBX/GLB 단독 재배포** | **불가** — 게임과 분리해 배포하지 않는다 |

마지막 항목이 실무에서 가장 중요하다. 모델 파일 자체를 에셋으로 내놓거나 공유
저장소에 올리는 것은 이 기록의 범위 밖이다. 게임의 일부로 배포하는 것만 해당한다.

### 보관 중인 증빙

- [x] Tripo 구독 영수증 — 스크린샷으로 별도 보관
- [x] Tripo 생성 목록 — 스크린샷으로 별도 보관
- [ ] Tripo 이용약관 사본 (그 시점 판본). 약관은 바뀌므로 **지금** 받아 둔다
- [ ] 원화 원본 파일

원화 원본이 남아 있다면 함께 보관한다. 순수 AI 생성물은 저작권 보호를 받기 어렵다는
것이 현재 판단이고, 이 프로젝트의 주장이 단단한 이유는 **사람이 만든 입력이 있고
생성 후에도 사람이 손을 댔다**는 것이기 때문이다. 그 사실을 보이는 자료다.

증빙은 저장소에 넣지 않는다. 결제 정보가 들어 있고, 저장소는 팀 밖으로 나갈 수 있다.

### 저작권 주장에 관한 참고

순수 AI 생성물은 한국·미국 모두 저작권 보호를 받기 어렵다는 것이 현재 판단이다.
다만 이 프로젝트는 **사람이 만든 원화를 입력으로 썼고 생성 후에도 사람이 수정**했다.
사람의 창작이 앞뒤에 모두 있으므로 위 주장은 순수 생성물보다 훨씬 단단하다.

그래서 **원화 원본 보관이 증빙 목록에서 가장 중요하다.** 그것이 사람의 창작을 보이는
유일한 자료다.

공모전 규정에 AI 생성물 사용 가능 여부와 제출물 권리 귀속 조항이 있는지는 별도로
확인해야 한다. 이건 기술이 아니라 규정 문제다 (`DOC-004`).

## 2. 효과음

**10개 전부 CC0**로, freesound.org에서 CC0 필터로만 받았다. CC0는 표기 의무가 없다.

파일별 목록은 [docs/17_AUDIO_CREDITS.md](docs/17_AUDIO_CREDITS.md)에 있다. URL은
기록하지 않았다 — CC0라 의무는 없지만, 다음에 받는 것은 URL도 함께 적는다.

## 3. 폰트

지금 UI는 Unity 내장 폰트 `LegacyRuntime.ttf`를 쓴다
(`Resources.GetBuiltinResource<Font>`). 엔진에 포함된 것이라 별도 라이선스나 표기가
필요 없고, 저장소에 폰트 파일을 넣은 적이 없다.

**WebGL로 제출하기로 했으므로 한글 폰트를 넣어야 한다.** 내장 폰트에는 한글
글리프가 없다. Windows 빌드에서 한글이 보이는 것은 OS 폰트로 대체되기 때문이고,
WebGL에는 그 대체가 없어서 한글이 전부 네모로 나온다.

**OFL 폰트로 진행하기로 했다.** 둘 다 임베딩과 재배포가 허용되고 상업적 사용에
제약이 없다.

| 후보 | 라이선스 | 비고 |
|---|---|---|
| 프리텐다드 (Pretendard) | OFL 1.1 | UI에 적합. 굵기 선택지가 많다 |
| 본고딕 (Source Han Sans / Noto Sans KR) | OFL 1.1 | 무난하고 글리프가 넓다 |

넣을 때 이 표에 **실제 채택한 폰트와 버전, 받은 URL**을 적는다. OFL은 표기 의무가
없지만 폰트 파일 자체를 재배포하는 형태이므로 출처가 남아 있어야 한다.

> WebGL은 폰트 아틀라스 크기가 곧 빌드 용량이다. 한글 전체(약 11,172자)를 굽지 말고
> **실제 쓰는 글자만** 넣거나 동적 폰트로 두는 편이 낫다.

## 4. 엔진과 패키지

Unity `6000.5.4f1` (URP `17.5.0`). 아래는 Unity 공식 패키지이며 Unity 라이선스가
적용된다.

| 패키지 | 버전 |
|---|---|
| `com.unity.netcode.gameobjects` | 2.13.0 |
| `com.unity.cinemachine` | 3.1.7 |
| `com.unity.inputsystem` | 1.19.0 |
| `com.unity.ai.navigation` | 2.0.13 |
| `com.unity.postprocessing` | 3.5.4 |
| `com.unity.render-pipelines.universal` | 17.5.0 |
| `com.unity.ugui`, `com.unity.timeline` 등 | manifest 참조 |

전체 목록은 `Packages/manifest.json`에 있다.

## 5. 저장소에 포함되지 않은 것

**TopDownEngine** (`Assets/TopDownEngine/`)은 **라이선스가 재배포를 금지**하므로
저장소에서 제외돼 있고 개발 PC에만 로컬로 있다. 빌드는 이것 없이 되도록 되어 있다
(`CATCOPS_TOPDOWNENGINE` 정의로 격리). 제출물에도 포함되지 않는다.

## 6. 배경음악 — Suno AI로 정했다

**유료 구독 기간에 생성한 것만 상업적 사용이 가능하다.** 무료(Basic) 생성물은
비상업 용도로 제한되므로 제출물에 쓸 수 없다.

그래서 **모델과 똑같이 증빙이 필요하다.** "이 곡이 유료 기간에 만들어졌다"를 나중에
증명할 방법이 그것뿐이다.

- [ ] Suno 구독 영수증 (결제일과 요금제가 보이는 것)
- [ ] 각 트랙의 생성 시각
- [ ] Suno 이용약관 그 시점 판본

트랙이 들어오면 위 표와 `docs/17`에 트랙명·용도·요금제·생성일을 적는다.
**효과음의 "전부 CC0"는 그 시점부터 전체를 설명하지 못한다.**

Pixabay는 쓰지 않기로 했다. 검토한 곡이 Content ID에 등록돼 있어 플레이 영상에
저작권 클레임이 붙을 수 있었다.

## 7. OpenAI API — 자산이 아니라 서비스

음성 인식과 명령 분류를 런타임에 OpenAI API로 처리한다 (`server/`). 제출물에
포함되는 파일이 아니라 **실행 중 호출하는 외부 서비스**라 성격이 다르다.

| 항목 | 내용 |
|---|---|
| 용도 | 음성 → 텍스트, 텍스트 → 게임 명령 분류 |
| 위치 | `server/src/voice/` |
| 키 | `server/.env` — **저장소에 넣지 않는다** |
| 데이터 | 플레이어 음성이 OpenAI로 전송된다 |

마지막 줄이 중요하다. **플레이어의 음성이 외부로 나간다면 그 사실을 알려야 한다.**
공모전 제출물이 실제 사용자를 받는다면 게임 안에 한 줄 고지가 필요하고, 이것은
라이선스가 아니라 개인정보 문제다.

`SUBMIT-004`(AI 활용 문서)에는 이것과 Tripo, Suno를 **구분해서** 적는다. 셋의 성격이
다르다 — 하나는 실행 중 호출하는 서비스, 둘은 제작에 쓴 생성 도구다.

## 갱신 규칙

1. 자산을 추가하는 변경에서 **이 문서를 같이 고친다.** 나중에 몰아서 하면 어느 파일이
   어느 출처였는지 복원할 수 없다.
2. 출처를 모르는 파일은 **저장소에 남기지 않는다.** 지우거나, 출처를 확인하고 적는다.
3. CC-BY처럼 표기 의무가 있는 것을 받으면 작성자·파일명·URL을 반드시 적는다.
4. 새 생성 모델은 Tripo 생성 이력에 남는지 확인하고 증빙을 함께 보관한다.

## 8. Windows LocalAI runtime

The Windows Standalone local voice path uses a loopback-only LocalAI Gateway at
`127.0.0.1`. It is an installation/runtime artifact, not a cloud service, and
does not send audio, transcripts, or command context to an external server.
Temporary WAV files are deleted after processing unless development debug
recording is explicitly enabled.

| Component | Purpose | License note |
|---|---|---|
| `faster-whisper` | Local STT runtime used by the gateway | MIT; see `Licenses/faster-whisper-MIT.txt` |
| `Systran/faster-whisper-small` | Local STT model | MIT; see `Licenses/faster-whisper-small-MIT.txt` |
| `Ollama` | Local model runner for command interpretation fallback | MIT; see `Licenses/Ollama-license.txt` |
| `Qwen3` | Local instruction model used through Ollama | Apache-2.0; see `Licenses/Qwen3-Apache-2.0.txt` |
