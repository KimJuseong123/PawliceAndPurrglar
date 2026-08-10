# MVP 범위

## 1. 검증 질문

현재 프로토타입은 다음 질문에 답해야 한다.

> 음성으로 동물 파트너에게 명령하면서 상대를 추적하거나 따돌리는 플레이가 실제로 재미있는가?

실제 음성 입력과 최종 모델은 이 질문에 긍정적인 답을 얻은 뒤 연결한다.

## 2. 단계 구분

### 단계 A: 핵심 프로토타입

현재 구현 대상이다.

### 단계 B: 공모전 제출 MVP

단계 A가 재미와 안정성을 통과한 뒤 음성, 네트워크, 최종 표현을 추가한 제출 대상이다.

## 3. 단계 A Must Have

- 3D 기울어진 탑다운 카메라
- 그레이박스 마을 1개
- 경찰과 도둑 이동
- 역할별 시작 위치와 목표
- 4분 타이머
- 보물 1개 이상
- 보물 획득, 운반, 판매
- 너구리 상인 판매 지점
- 체포 시작, 중단, 완료
- 최소 승패 판정
- 강아지 추적 명령 1개
- 고양이 교란 명령 1개
- 음성 명령 입력 (`V` 푸시투토크)
- 명령 상태와 실패 피드백
- 결과 화면과 재시작
- 한 경기를 처음부터 끝까지 완료
- 경찰 모델 1체의 리깅 파이프라인 검증
- 경찰 `Idle`, `Run`, `ComedyRun` 전환 검증

## 4. 단계 A Should Have

- 강아지와 고양이의 명령 각 4개
- 명령 쿨타임
- 대시
- 투척물 1종
- 바나나 미끄러짐 또는 동등한 코믹 방해 1종
- 역할별 HUD
- 건물 가림 투명화
- 역할 교대 재경기
- 기본 효과음

## 5. 단계 A Won't Have

- 실제 마이크 입력
- STT 서비스
- 자연어 분류
- 자유 대화 AI
- 본격 네트워크 멀티플레이
- 최종 캐릭터, 동물, 건물 모델
- 여러 맵
- 계정, 매칭, 랭킹
- 성장, 장비, 결제
- 모바일 조작

단, 제품 기능과 분리된 `TECH-003` 장면에서는 Windows 마이크와 STT의
기술 가능성, 권한 실패와 대체 입력만 사전 검증할 수 있다. 이 장면의 텍스트는
동물 명령, 자연어 분류 또는 게임 상태에 연결하지 않는다.

## 6. 단계 B 제출 목표

단계 A 완료 후 우선순위를 다시 평가한다.

- 경찰 1명 대 도둑 1명 플레이 방식
- 확정된 네트워크 또는 로컬 시연 구조
- 전체 동물 명령
- 실제 음성 입력 또는 심사 환경에서 재현 가능한 음성 시연
- 음성 실패 시 키보드와 버튼 대체 입력
- 음성 인식 결과와 실패 피드백
- 최종 또는 준최종 캐릭터와 동물 모델
- 역할과 명령이 읽히는 애니메이션
- 효과음, 배경음, 핵심 이펙트
- 튜토리얼 또는 첫 경기 안내
- 실행 가능한 제출 빌드
- 30~60초 안에 핵심이 보이는 플레이 영상

음성 기능을 제출 Must Have로 확정하는 시점은 기술 위험 검증 후 `docs/14_DECISION_LOG.md`에 기록한다.

## 7. MVP에서 제외

- 3명 이상 멀티플레이
- 랭크 매칭
- 사용자 계정
- 인게임 결제
- 성장과 장비
- 여러 캐릭터 선택
- 여러 맵
- 자유 대화형 반려동물
- 런타임 생성형 AI 판단
- 복잡한 물리 파괴
- 절차적 맵 생성
- 리플레이 시스템
- 관전자 개입

## 8. 단계 A 완료 시나리오

1. 경찰과 도둑이 올바른 위치에서 시작한다.
2. 4분 타이머가 시작된다.
3. 도둑이 보물을 하나 획득한다.
4. 경찰이 음성으로 강아지 추적을 명령한다.
5. 도둑이 음성으로 고양이 교란을 명령한다.
6. 도둑이 너구리 상인에게 보물을 판매할 수 있다.
7. 경찰 체포 또는 도둑 목표 달성으로 경기가 종료된다.
8. 결과 화면에서 승리 이유를 확인할 수 있다.
9. 재시작 후 이전 경기 상태가 남지 않는다.
10. 경찰 모델의 `Idle`, `Run`, `ComedyRun`이 이동 상태에 맞게 전환된다.

## 9. 범위 변경 규칙

- 새 기능을 추가하기 전에 기존 Must Have의 완료 여부를 확인한다.
- 음성, 네트워크, 최종 모델링은 별도의 에픽으로 유지한다.
- 미확정 밸런스 수치를 완료 조건으로 고정하지 않는다.
- 범위 변경은 `docs/14_DECISION_LOG.md`와 이 문서에 함께 기록한다.

## Windows Local AI Addendum

Windows Standalone voice input uses Unity `Microphone` and a bundled loopback
LocalAI Gateway. The Gateway uses `Systran/faster-whisper-small` and Ollama
`qwen3:4b-instruct`. Numeric-key and button commands remain available when
local services, models, or microphone permission are unavailable. Speech
accuracy and gameplay verification remain manual checks.
# Voice Command MVP Addendum

The voice-command MVP is now implemented as a WebGL client adapter plus a
same-repository Fastify service. Browser audio is capped at five seconds and
five megabytes. The service performs transcription and intent candidate
extraction; the Unity NGO Host remains authoritative for target validation,
Pet Cognition, random outcomes, and companion actions.

Voice failures must not disable numeric-key or button commands. Original audio
is not persisted by default. The MVP does not include streaming STT, model
fine-tuning, a database, or a replacement network transport.

## Local WebGL Playtest Addendum

The playable MVP includes a local one-player WebGL launch path through
`play-webgl.bat`. Keyboard commands remain the fallback. When the voice API is
configured, the offline game registers a temporary voice capability session so
the browser microphone can exercise the existing STT and intent pipeline.
