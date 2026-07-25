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
| MATCH-004 | P0 | TODO | 승리 판정 |
| MATCH-005 | P0 | TODO | 단일 경기 종료 |
| MATCH-006 | P0 | TODO | 결과 화면 |
| MATCH-007 | P0 | TODO | 재시작 초기화 |

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
| LOOT-007 | P0 | TODO | 중복 획득·판매 방지 |
| MERCHANT-001 | P0 | TODO | 너구리 상인 판매 범위 |
| MERCHANT-002 | P0 | TODO | 중복 판매 방지 |
| MERCHANT-003 | P1 | TODO | 판매 피드백 |
| MERCHANT-004 | P2 | TODO | 출현 위치 규칙 |

## Epic 7. 체포와 방해

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| ARREST-001 | P0 | TODO | 체포 범위 감지 |
| ARREST-002 | P0 | TODO | 체포 진행과 중단 |
| ARREST-003 | P0 | TODO | 체포 완료 |
| ARREST-004 | P0 | TODO | 체포 UI |
| THROW-001 | P1 | TODO | 소품 줍기와 던지기 |
| THROW-002 | P1 | TODO | 투척 궤적과 착탄 |
| THROW-003 | P1 | TODO | 바나나 미끄러짐 |
| THROW-004 | P2 | TODO | 동물 혼란용 소품 |

## Epic 8. 동물 파트너

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| COMP-001 | P0 | TODO | 공통 명령 요청과 검증 |
| COMP-002 | P0 | TODO | 숫자키 `1`~`4` 입력 어댑터 |
| COMP-003 | P0 | TODO | 소유자 따라가기 |
| COMP-004 | P0 | TODO | 경로 실패 복구 |
| DOG-001 | P0 | TODO | `TRACK` |
| DOG-002 | P1 | TODO | `SEARCH` |
| DOG-003 | P1 | TODO | `GUARD` |
| DOG-004 | P1 | TODO | `BARK` |
| CAT-001 | P0 | TODO | `DISTRACT` |
| CAT-002 | P1 | TODO | `SCOUT` |
| CAT-003 | P2 | TODO | `STEAL` |
| CAT-004 | P2 | TODO | `HIDE` |

## Epic 9. UI

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| UI-001 | P0 | DONE | 공통 HUD |
| UI-002 | P0 | TODO | 경찰 HUD |
| UI-003 | P0 | TODO | 도둑 HUD |
| UI-004 | P0 | TODO | 명령 슬롯과 쿨타임 |
| UI-005 | P0 | TODO | 명령 성공과 실패 |
| UI-006 | P0 | TODO | 체포 게이지 |
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

## Epic 12. 최종 아트와 제출

| ID | 우선순위 | 상태 | 작업 |
|---|---|---|---|
| ART-001 | P2 | DONE | 캐릭터 VisualRoot 교체 구조 |
| ART-002 | P2 | BLOCKED | 건물과 소품 모델 |
| AUDIO-001 | P2 | TODO | 임시 규칙 피드백 효과음 |
| SUBMIT-001 | P2 | TODO | 목표 플랫폼 빌드 |
| SUBMIT-002 | P2 | TODO | 플레이 영상 |
| SUBMIT-003 | P2 | TODO | 게임 소개 문서 |
| SUBMIT-004 | P2 | TODO | AI 활용 문서 |
| SUBMIT-005 | P2 | TODO | 라이선스 정리 |

## 바로 다음 작업

1. `LOOT-002` 보물 획득
2. `LOOT-003` 보물 운반
3. `LOOT-004` 보물 드롭
