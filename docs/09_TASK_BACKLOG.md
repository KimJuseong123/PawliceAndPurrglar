# 작업 백로그

## 상태

- `TODO`
- `READY`
- `IN_PROGRESS`
- `BLOCKED`
- `REVIEW`
- `DONE`

## 우선순위

- `P0`: 현재 단계 완료에 필수
- `P1`: 핵심 경험 향상
- `P2`: 다음 단계 준비
- `P3`: MVP 이후

## 현재 작업 원칙

- 한 번에 한 작업만 `IN_PROGRESS`로 둔다.
- 단계 A 프로토타입이 끝나기 전 음성과 최종 모델 작업을 시작하지 않는다.
- 작업 완료 시 `docs/13_CURRENT_STATE.md`를 갱신한다.
- 결정 변경 시 `docs/14_DECISION_LOG.md`를 갱신한다.

## Epic 0. 문서

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| DOC-001 | P0 | DONE | AGENTS와 README 작성 |
| DOC-002 | P0 | DONE | 프로젝트 문서 패키지 작성 |
| DOC-003 | P1 | TODO | 팀 리뷰 후 미확정 규칙 정리 |
| DOC-004 | P1 | TODO | 공모전 요구 양식 반영 |

## Epic 1. 프로젝트 기반

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| BASE-001 | P0 | DONE | 목표 저장소 폴더 구조 생성 |
| BASE-002 | P0 | DONE | Unity 버전과 패키지 고정 |
| BASE-003 | P0 | DONE | `Bootstrap`, `Game`, `Result` 기본 씬 구성 |
| BASE-004 | P0 | DONE | 공통 설정 구조 생성 |
| BASE-005 | P0 | DONE | 로깅과 오류 처리 |
| BASE-006 | P1 | DONE | Edit Mode 및 Play Mode 테스트 어셈블리 |

## Epic 1.5. 고위험 기술 검증

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| TECH-001 | P0 | DONE | Windows 목표 플랫폼 빌드와 키보드 이동 검증 |
| TECH-002 | P0 | DONE | Blender 모델, 리그, 애니메이션 연동 검증 |
| TECH-003 | P1 | BLOCKED | Windows 마이크와 음성 텍스트 변환 격리 검증 |

## Epic 2. 카메라와 맵

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| CAMERA-001 | P0 | TODO | 3D 기울어진 탑다운 추적 카메라 |
| CAMERA-002 | P1 | TODO | 추격 속도 기반 줌 |
| CAMERA-003 | P1 | TODO | 지붕과 벽 가림 처리 |
| MAP-001 | P0 | DONE | 순환형 그레이박스 마을 |
| MAP-002 | P1 | TODO | 주요 장소별 복수 경로 |
| MAP-003 | P2 | TODO | 지붕과 사다리 경로 |

## Epic 3. 경찰 리깅 스파이크

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| RIG-001 | P0 | READY | 기존 경찰 모델 축, 단위, 피벗 점검 |
| RIG-002 | P0 | TODO | 경찰 뼈 계층과 스킨 제작 |
| RIG-003 | P0 | TODO | Humanoid Avatar 검증 |
| ANIM-001 | P0 | TODO | `Idle` 테스트 |
| ANIM-002 | P0 | TODO | `Run` 테스트 |
| ANIM-003 | P0 | TODO | `ComedyRun` 테스트 |
| ANIM-004 | P1 | TODO | 코드 이동과 Animator 전환 |
| ANIM-005 | P2 | TODO | `Throw`, `Hit`, `Slip` 후보 |

## Epic 4. 경기 시스템

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| MATCH-001 | P0 | DONE | 경기 상태 정의 |
| MATCH-002 | P0 | DONE | 준비 카운트다운 |
| MATCH-003 | P0 | DONE | 4분 타이머 |
| MATCH-004 | P0 | DONE | 승리 판정 |
| MATCH-005 | P0 | DONE | 단일 경기 종료 |
| MATCH-006 | P0 | DONE | 결과 화면 |
| MATCH-007 | P0 | DONE | 재시작 초기화 |

## Epic 5. 플레이어

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| PLAYER-001 | P0 | DONE | 경찰 이동 |
| PLAYER-002 | P0 | DONE | 도둑 이동 |
| PLAYER-003 | P1 | DONE | 대시 |
| PLAYER-004 | P0 | DONE | 공통 상호작용 |
| PLAYER-005 | P0 | DONE | 역할 구분 |
| PLAYER-006 | P1 | DONE | 보물 소지 이동 페널티 |

## Epic 6. 보물과 너구리 상인

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| LOOT-001 | P0 | DONE | 보물 상태 정의 |
| LOOT-008 | P0 | DONE | 보물 가격 데이터 |
| LOOT-002 | P0 | DONE | 보물 획득 |
| LOOT-003 | P0 | DONE | 운반 |
| LOOT-004 | P1 | DONE | 드롭 |
| LOOT-005 | P2 | TODO | 숨기기 |
| LOOT-006 | P0 | DONE | 보물 판매 |
| LOOT-007 | P0 | DONE | 중복 획득·판매 방지 |
| MERCHANT-001 | P0 | TODO | 너구리 상인 판매 범위 |
| MERCHANT-002 | P0 | TODO | 중복 판매 방지 |
| MERCHANT-003 | P1 | TODO | 판매 피드백 |
| MERCHANT-004 | P2 | TODO | 출현 위치 규칙 |

## Epic 7. 체포와 방해

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| ARREST-001 | P0 | DONE | 체포 범위 감지 |
| ARREST-002 | P0 | DONE | 체포 진행도 |
| ARREST-003 | P0 | DONE | 체포 중단 |
| ARREST-004 | P0 | DONE | 체포 완료 |
| ARREST-005 | P0 | DONE | 체포 UI |
| THROW-001 | P1 | TODO | 소품 줍기와 던지기 |
| THROW-002 | P1 | TODO | 투척 궤적과 착탄 |
| THROW-003 | P1 | TODO | 바나나 미끄러짐 |
| THROW-004 | P2 | TODO | 동물 혼란용 소품 |

## Epic 8. 동물 파트너

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| COMP-001 | P0 | DONE | 공통 AI 동료 상태 구조 (7개 상태) |
| COMP-002 | P0 | DONE | 소유자 따라가기와 리쉬 복구 |
| COMP-003 | P0 | DONE | 명령 요청 데이터 구조 |
| COMP-004 | P0 | DONE | 명령 전달기 (입력과 AI 분리) |
| COMP-005 | P0 | DONE | 명령 유효성 검사 |
| COMP-006 | P0 | DONE | 경로 실패 복구 |
| COMP-007 | P0 | DONE | 숫자키 명령의 실제 효과 (`COMP-008` 해소, `DOG-003`·`CAT-004`로 대체) |
| COMP-008 | P0 | DONE | 동료 벽 충돌 (`CharacterController`) |

### 단계 9. 강아지와 고양이 최소 명령

처음부터 8개를 만들지 않는다. 대표 명령 하나씩으로 재미를 먼저 검증한다.

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| DOG-001 | P0 | DONE | 강아지 소유와 따라가기 |
| CAT-001 | P0 | DONE | 고양이 소유와 따라가기 |
| DOG-002 | P0 | DONE | 강아지 명령 수신 (경찰 진영만) |
| CAT-002 | P0 | DONE | 고양이 명령 수신 (도둑 진영만) |
| DOG-007 | P1 | DONE | 강아지 명령 쿨타임 |
| CAT-007 | P1 | DONE | 고양이 명령 쿨타임 |
| DOG-008 | P1 | DONE | 강아지 경로 실패 복구 설정 |
| CAT-008 | P1 | DONE | 고양이 경로 실패 복구 설정 |
| UI-004 | P0 | DONE | 명령 버튼 (경찰 추적 / 도둑 교란) |
| VOICE-007 | P0 | DONE | 버튼·숫자키가 `CommandRequest` 생성 |
| DOG-003 | P0 | DONE | 추적 (흔적 기반, 실제 위치를 완벽히 알지 않음) |
| CAT-004 | P0 | DONE | 교란 (정보만, 실제 위치 불변, 지속시간 제한, 중복 방지) |
| UI-005 | P0 | DONE | 명령 쿨타임 표시 |
| UI-006 | P0 | DONE | 명령 결과 피드백 |

### 단계 10. 나머지 AI 명령

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| DOG-004 | P1 | DONE | 수색 (지정 지역 이동) |
| DOG-005 | P1 | DONE | 경계 (지정 위치 유지) |
| DOG-006 | P1 | DONE | 짖기 (근접 시 도둑 노출, 아니면 실패 보고) |
| CAT-003 | P1 | DONE | 정찰 (주변 보물·경찰 보고, 상태 변경 없음) |
| CAT-005 | P1 | DONE | 절도 (보물로 이동, 판매는 수행하지 않음) |
| LOOT-005 | P1 | DONE | 보물 숨기기 공통 기능 (지정 은신처 2곳) |
| CAT-006 | P1 | DONE | 숨기 (가까운 은신처로 이동) |
| COMP-007 | P1 | DONE | 귀여운 대기 행동 (승패·쿨타임 영향 없음) |

`DOG-004`와 `DOG-005`는 현재 "지정 지점으로 이동하고 보고"까지만 한다. 원형
탐색 경로나 경계 중 침입 감지 같은 고유 로직은 없으므로 이를 "수색·경계 완성"
으로 표현하지 않는다.

`CAT-005` 절도는 고양이가 보물 위치까지 이동하는 것까지만이다. 실제 집어
오기와 소유자에게 전달은 미구현이며, 판매는 의도적으로 구현하지 않는다.

## Epic 9. UI

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| UI-001 | P0 | DONE | 공통 HUD |
| UI-002 | P0 | DONE | 경찰 HUD |
| UI-003 | P0 | DONE | 도둑 HUD |
| UI-004 | P0 | TODO | 명령 슬롯과 쿨타임 |
| UI-005 | P0 | TODO | 명령 성공과 실패 |
| UI-006 | P0 | DONE | 체포 게이지 (`ARREST-005`에 통합) |
| UI-007 | P0 | DONE | 역할별 목표 안내 |
| UI-008 | P1 | TODO | 첫 경기 안내 |

## Epic 10. 멀티플레이

NET-001과 NET-002는 본격 멀티플레이가 아니라 패키지와 권한 구조를 결정하기
위한 격리된 기술 검증이다. 이후 게임 기능 동기화는 검증 결과 채택 전
`BLOCKED`다.

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| NET-001 | P0 | DONE | Host·Client 접속, 플레이어 생성, 위치 확인과 종료 처리 |
| NET-002 | P0 | DONE | 경찰·도둑 역할 배정과 시작 위치 |
| NET-003 | P2 | BLOCKED | 이동 동기화 |
| NET-004 | P2 | BLOCKED | 보물 소유권 |
| NET-005 | P2 | BLOCKED | 판매, 체포, 결과 판정 |

## 기술 검증 관문 A

| 검증 항목 | 상태 | 근거 |
|---|---|---|
| Unity 목표 플랫폼 빌드 | PASS | TECH-001 Windows x86_64 |
| Blender 테스트 모델 임포트 | PASS | TECH-002 Generic 5본, `Idle`·`Walk` |
| 음성 텍스트 출력 | BLOCKED | TECH-003 `0x80004003`, 실제 발화 미검증 |
| 두 플레이어 접속 | PASS | NET-001 Host·Client 결과 통과 |
| 경찰·도둑 역할 배정 | PASS | NET-002 양쪽 결과 통과 |
| 관문 A 전체 | BLOCKED | 음성 텍스트 출력 미통과 |

## Epic 11. 음성

핵심 프로토타입 완료 전 구현하지 않는다.

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| VOICE-001 | P2 | BLOCKED | STT 인터페이스 |
| VOICE-002 | P2 | BLOCKED | 텍스트 정규화 |
| VOICE-003 | P2 | BLOCKED | 경찰 명령 분류 |
| VOICE-004 | P2 | BLOCKED | 도둑 명령 분류 |
| VOICE-005 | P2 | BLOCKED | 신뢰도 처리 |
| VOICE-006 | P2 | BLOCKED | 음성 피드백 UI |
| VOICE-007 | P2 | BLOCKED | 마이크 거부와 실패 대응 |

## Epic 12. Blender 최종 모델 제작과 적용

Blender 모델 자체는 작업자가 제작한다. 이 저장소의 작업은 Unity 임포트,
프리팹, Animator, 최적화다.

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| MODEL-001 | P1 | TODO | 캐릭터 제작 규격 확정 (단위, 축, 키, 본 이름, 텍스처, 폴리곤, 클립 이름) |
| MODEL-002 | P1 | TODO | 대표 캐릭터 한 세트 제작 (경찰+강아지 또는 도둑+고양이) |
| ART-002 | P1 | TODO | Unity 임포트 규칙 검사 도구 (스케일, 클립 이름, 머티리얼 위치, 경고) |
| ART-003 | P1 | TODO | 기본 Animator 구성 (`Idle`, `Walk`, `Run`, `Command`, `Win`, `Lose`) |
| ART-004 | P1 | TODO | 대표 모델 게임 적용 (Collider·이동 속도·체포 거리 유지) |
| ART-005 | P1 | DONE | 대표 모델 성능 측정 (드로우 콜 제외, 아래 결과) |
| MODEL-003 | P2 | TODO | 경찰 모델 최종 제작 |
| MODEL-004 | P2 | TODO | 도둑 모델 최종 제작 |
| MODEL-005 | P2 | TODO | 강아지 모델 최종 제작 |
| MODEL-006 | P2 | TODO | 고양이 모델 최종 제작 |
| MODEL-007 | P2 | TODO | 건물과 환경 모델 (보석상 → 슈퍼마켓 → 서점 → 너구리 장터 → 경찰서 → 사다리·지붕 → 쓰레기통 → 장식) |
| ART-006 | P2 | TODO | 경찰 모델 적용 |
| ART-007 | P2 | TODO | 도둑 모델 적용 |
| ART-008 | P2 | TODO | 강아지 모델 적용 |
| ART-009 | P2 | TODO | 고양이 모델 적용 |
| ART-010 | P2 | TODO | 환경 모델 적용 |
| MAP-004 | P2 | TODO | 맵 루트 밸런스 측정 |
| MAP-005 | P2 | TODO | 보물과 스폰 위치 확정 |
| ART-011 | P2 | TODO | 시각 효과 (명령 성공·실패, 획득, 판매, 냄새 흔적, 교란, 체포, 승리) |
| ART-012 | P3 | TODO | 모델과 머티리얼 최적화 (머티리얼 통합, 아틀라스, LOD, 애니메이션 압축) |

`MAP-002` 최종 충돌 구조와 `MAP-003` 지붕·사다리 루트는 Epic 2에서 관리한다.

### ART-000 임시 모델 적용 (완료)

프로토타입 테스트를 진행하기 위한 임시 조치이며 최종 아트가 아니다.

- 작업자가 제공한 프롭 `.blend` 19종을 FBX로 반입해 보물, 너구리 장터,
  쓰레기통, 사다리에 적용
- 캐릭터 모델은 아직 없으므로 외부 TopDown Engine의 `LoftTie`와
  `LoftSuspenders` 메시를 역할 색 머티리얼로 임시 대체
- 건물은 계속 회색 상자를 사용

## Epic 12.5. 제출

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| ART-001 | P2 | DONE | 캐릭터 VisualRoot 교체 구조 |
| AUDIO-001 | P2 | TODO | 임시 규칙 피드백 효과음 |
| SUBMIT-001 | P2 | TODO | 목표 플랫폼 빌드 |
| SUBMIT-002 | P2 | TODO | 플레이 영상 |
| SUBMIT-003 | P2 | TODO | 게임 소개 문서 |
| SUBMIT-004 | P2 | TODO | AI 활용 문서 |
| SUBMIT-005 | P2 | TODO | 라이선스 정리 |

## 프로토타입 관문 B: 첫 번째 수직 슬라이스

다음 시나리오가 완성돼야 한다.

```text
경기 시작
→ 경찰과 도둑 이동
→ 도둑이 보물 획득
→ 도둑이 판매처로 이동
→ 경찰이 도둑을 추격
→ 판매 또는 체포
→ 결과 화면
→ 재경기
```

강아지, 고양이, 음성, 최종 모델 없이 통과해야 한다. 이 단계에서 먼저
플레이테스트하며, 기본 추격전이 재미없으면 맵과 규칙을 수정한다.

| 검증 항목 | 상태 | 근거 |
|---|---|---|
| 경기 시작과 READY 카운트다운 | PASS | MATCH-002 |
| 경찰·도둑 이동 | PASS | PLAYER-001, PLAYER-002 |
| 보물 획득과 운반 | PASS | LOOT-002, LOOT-003 |
| 판매 | PASS | LOOT-006 |
| 체포 | PASS | ARREST-001~005 |
| 결과 화면 | PASS | MATCH-006 |
| 재경기 | PASS | MATCH-007 |
| 도둑 판매 승리 경로 | BLOCKED | `ISSUE-011` 보물 1개 200골드 대 목표 1,000골드 |
| 경찰 대 도둑 실제 추격 | BLOCKED | 로컬 1인 조작, 네트워크 동기화 `NET-003` 미채택 |
| 관문 B 전체 | BLOCKED | 위 두 항목 |

## 바로 다음 작업

1. `ISSUE-011` 도둑 판매 승리 경로 확보 (보물 수 또는 목표 금액 결정)
2. 관문 B 플레이테스트
3. `COMP-001` 공통 명령 요청과 검증
4. `COMP-002` 숫자키 `1`~`4` 입력 어댑터
