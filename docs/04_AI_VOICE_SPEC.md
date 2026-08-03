# 동물 AI 및 음성 명령 사양

## 1. 목적

자연어 또는 대체 입력을 제한된 게임 명령으로 변환해 예측 가능한 동물 행동을 실행한다.
자유 대화보다 빠른 반응, 명확한 결과, 실패 복구를 우선한다.

## 2. 단계별 구현

### 현재 단계

```text
Keyboard 1~4
-> Input Adapter
-> CompanionCommandId
-> Command Validator
-> Companion State Machine
-> Feedback UI
```

현재는 마이크, STT, 자연어 분류를 구현하지 않는다.

예외적으로 `TECH-003`은 Windows 음성 입력이 텍스트를 반환하는지 확인하는
격리된 기술 검증이다. 인식 결과를 `CompanionCommandId`로 바꾸거나 AI 대화를
호출하지 않으며, 프로토타입 입력은 계속 숫자키를 사용한다.

### 향후 음성 단계

```text
Microphone
-> Speech To Text
-> Text Normalizer
-> Intent Classifier
-> CompanionCommandId
-> Command Validator
-> Companion State Machine
-> Feedback UI
```

키보드와 음성은 `CompanionCommandId` 이후의 코드를 공유한다.

## 3. 명령 요청

명령 요청은 최소한 다음 정보를 가진다.

```text
CompanionCommandRequest
- commandId
- issuerRole
- companionId
- targetEntityId
- targetPosition
- issuedAt
- source
```

`source` 후보:

- `KEYBOARD`
- `UI_BUTTON`
- `GAMEPAD`
- `VOICE`

## 4. 경찰 강아지 명령

### TRACK

목적: 최근 도둑 흔적 또는 마지막 유효 위치를 추적한다.

현재 대체 입력: `1`

향후 음성 예:

- 도둑 따라가
- 냄새 맡아
- 흔적 찾아
- 쟤 추적해

실패 예:

- 유효한 흔적이 없음
- 강아지가 행동 불능
- 쿨타임 중

### SEARCH

목적: 지정한 주변 영역을 수색한다.

현재 대체 입력: `2`

향후 음성 예:

- 여기 찾아봐
- 이 주변 수색해
- 저 골목 확인해

### GUARD

목적: 지정 위치를 일정 시간 경계한다.

현재 대체 입력: `3`

향후 음성 예:

- 여기 지켜
- 입구 막아
- 움직이지 말고 기다려

### BARK

목적: 가까운 도둑을 압박하거나 일정 조건에서 위치를 드러낸다.

현재 대체 입력: `4`

향후 음성 예:

- 짖어
- 도둑 놀래켜
- 소리 내

## 5. 도둑 고양이 명령

### SCOUT

목적: 보물, 경찰 또는 안전 경로를 정찰한다.

현재 대체 입력: `1`

향후 음성 예:

- 보물 찾아
- 주변 살펴봐
- 경찰 어디 있어

### DISTRACT

목적: 소리 또는 가짜 흔적으로 경찰의 판단을 흔든다.

현재 대체 입력: `2`

향후 음성 예:

- 경찰 시선 끌어
- 저쪽에서 소리 내
- 경찰 유인해

### ROOF

목적: 근처 집 또는 상점의 유효한 지붕 포인트로 올라간다.

현재 대체 입력: `3`

향후 음성 예:

- 지붕으로 올라가
- 옥상 위로 가
- 가까운 집 위로 올라가

프로토타입 내부 구현은 기존 고양이 3번 명령 ID를 재사용하지만,
플레이어 표시명과 AI 의도명은 `ROOF`를 사용한다.

### HIDE

목적: 고양이 또는 소지 보물을 유효한 은신 지점에 숨긴다.

현재 대체 입력: `4`

향후 음성 예:

- 이거 숨겨
- 보물 감춰
- 여기 숨어

최종 대상과 효과는 미확정이다.

## 6. 상태 머신

공통 상태:

- `IDLE`
- `FOLLOW_OWNER`
- `MOVE_TO_TARGET`
- `EXECUTE_COMMAND`
- `RETURN_TO_OWNER`
- `COOLDOWN`
- `DISABLED`

강아지 행동 상태:

- `TRACKING`
- `SEARCHING`
- `GUARDING`
- `BARKING`

고양이 행동 상태:

- `SCOUTING`
- `DISTRACTING`
- `ROOFING`
- `HIDING`

명령이 끝나거나 실패하면 안전하게 소유자에게 복귀한다.
경로 탐색이 반복 실패하면 무한 재시도하지 않는다.

## 7. 명령 검증

검사 순서:

1. 경기 상태
2. 진영과 명령 소유권
3. 동물 존재와 활성 상태
4. 현재 상태의 중단 가능 여부
5. 쿨타임
6. 대상과 위치
7. 경로 가능성

실패한 명령은 기본적으로 쿨타임을 소비하지 않는다.
실패 이유는 기계가 읽을 수 있는 코드와 사용자 문구로 분리한다.

## 8. UI 피드백

현재 프로토타입:

```text
입력: 1
명령: 추적
결과: 실행 중
```

실패 예:

```text
입력: 1
명령: 추적
결과: 추적할 흔적이 없습니다.
```

향후 음성 통합:

```text
들은 말: "저 골목 찾아봐"
인식 명령: 주변 수색
결과: 실행 중
```

## 9. 향후 자연어 정규화

- 앞뒤와 반복 공백 제거
- 문장부호 제거
- 자주 발생하는 오인식 치환
- 감탄사와 호칭 일부 제거
- 동의어 사전 적용

낮은 신뢰도의 문장은 임의 실행하지 않는다.

- 높은 신뢰도: 즉시 실행
- 중간 신뢰도: 예상 명령 확인
- 낮은 신뢰도: 실행하지 않고 재입력 안내

신뢰도 기준은 STT 기술 선택 후 정한다.

## 10. 귀여운 돌발 행동

돌발 행동은 다음 조건에서만 허용한다.

- 핵심 명령을 방해하지 않는다.
- 승패와 정보 정확도를 바꾸지 않는다.
- 쿨타임을 소비하지 않는다.
- 이동 불능을 만들지 않는다.
- 대기 또는 명령 완료 이후에 실행한다.

## 11. 테스트 우선순위

현재:

- 키별 올바른 명령 ID
- 잘못된 진영 명령 거부
- 대상 없음
- 쿨타임
- 연속 입력
- 경로 실패와 복귀
- 경기 종료 중 명령 거부

향후:

- 동의어와 오인식
- 낮은 신뢰도
- 마이크 거부
- 서비스 실패
- 음성과 버튼의 동일한 결과
# Implemented Voice Pipeline

The production prototype path is:

`WebGL MediaRecorder -> Fastify multipart API -> STT -> exact command matcher /
Structured Intent candidates -> Host WebSocket -> PetCognitionResolver ->
existing CompanionCommandDispatcher`.

Absolute commands are resolved before the intent model. The Fastify service
only returns normalized text and bounded candidates. The Unity NGO Host
revalidates target IDs, faction, range, cooldown, and action availability before
executing anything. Dog and cat cognition are separate and use deterministic
Host-side seeds.
