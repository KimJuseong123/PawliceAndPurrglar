# Animation Clip 요청

현재 `CharacterLocomotion.controller`가 참조하던 Loft3D 원본 Animation Clip이 프로젝트에서 누락되어 있습니다.

필요한 상태는 다음과 같습니다.

- Idle
- Walk
- Run
- Command
- Win
- Lose

기존 Controller의 GUID 참조를 그대로 복구하려면 원본 FBX 또는 `.anim` 파일뿐 아니라 각 파일의 `.meta`도 함께 필요합니다.

UnityPackage로 전달할 경우 메타데이터가 포함되도록 Export Package를 사용하고, 원본 폴더 구조를 유지해 주세요.

새로운 대체 Clip을 전달하는 경우에는 현재 `CharacterLocomotion.controller`를 덮어쓰지 말고, Clip의 Rig 종류와 binding path가 명시된 상태로 전달해 주세요.

유료 TopDown Engine 또는 Loft3D Asset 파일이라면 라이선스상 팀원 간 공유와 저장소 포함이 가능한지도 확인해 주세요.

Clip을 받으면 다음 항목을 함께 기록합니다.

- 파일 경로와 GUID
- Rig type
- Loop Time
- Clip length
- 적용 캐릭터
- 적용 Controller
- Root Motion 사용 여부
- 기존 procedural fallback 활성 여부
