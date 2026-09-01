# TASK-002 — GameScope UI 전환 및 Legacy 기능 숨김

## 목적
기존 `Automation Framework` UI를 `GameScope AI` 용도로 정리한다.

기존 자동화 기능은 삭제하지 않고 기본 UI에서 숨긴다. 향후 필요 시 다시 표시할 수 있도록 `Legacy 기능 표시` 진입점을 제공한다. 이번 작업에서는 대규모 리팩터링을 하지 않는다.

## 현재 확인된 상태
기존 프로젝트에는 다음 기능이 존재하며 정상 동작을 확인했다.

- Window Capture / Background Capture
- Offline Image Load / Source Preview
- AOI Picker / AOI 좌표 계산
- Crop / Crop Preview

기존 기능을 다시 구현하지 않는다.

## 1. 제품명 변경
UI 표시를 다음으로 변경한다.

```text
GameScope AI
Game Screen Analysis & Vision Intelligence
```

MainWindow의 Window Title도 `GameScope AI`로 변경한다.

프로젝트명, namespace, assembly 이름은 이번 TASK에서 변경하지 않는다.

## 2. 기본 UI에서 유지
- Available Windows
- Refresh / Add Target / Remove Target / Activate Target
- Background Capture(Test)
- Offline Image
- Targets / AOIs
- Pick AOI / Add AOI / Update AOI / Delete AOI
- Capture Preview 관련 기능

기존 동작을 변경하지 않는다.

## 3. Legacy로 숨길 기능
다음 기능은 삭제하지 않고 GameScope 기본 화면에서 숨긴다.

- Macros
- Monitoring
- Trigger / Action 설정
- ImageMatch Template 설정
- Create Pattern
- Macro Action 설정
- Test AOI
- 전체 실행 / 전체 정지 / 자동화용 정지 기능

관련 코드, Service, Model, Window, 저장 구조는 삭제하지 않는다.

## 4. Legacy 표시 Toggle
MainWindow 상단에 다음 Toggle 또는 CheckBox를 추가한다.

```text
[ Legacy 기능 표시 ]
```

기본값은 OFF.

OFF에서는 Legacy UI가 보이지 않아야 하며 ON에서는 기존 Automation Framework UI를 다시 사용할 수 있어야 한다.

가능하면 XAML `Visibility`와 하나의 상태값으로 관리하고, 기능별 중복 Visibility 로직을 남발하지 않는다.

## 5. 기본 화면
Legacy OFF에서는 GameScope 핵심 기능 중심으로 보이게 한다.

```text
GameScope AI
Game Screen Analysis & Vision Intelligence

[Targets] [AOI / Vision]              [Legacy 기능 표시]

Available Windows
------------------------------------------------
Window List

[Background Capture] [Offline Image] [Refresh] [Add Target]

Targets / AOIs
------------------------------------------------
Target
 └─ AOI

[Activate Target] [Remove Target] [Pick AOI]
[Add AOI] [Update AOI] [Delete AOI]
```

현재 스타일을 전면 재설계하지 않는다.

## 6. Offline Image 기능 보호
다음 기존 기능을 그대로 유지한다.

```text
OfflineImageButton_Click
OfflineImageValidationWindow
AoiPickerWindow
CapturePreviewWindow
```

검증된 흐름:

```text
Image File
 → ImageFileCaptureService
 → Source Preview
 → AoiPickerWindow
 → WindowBounds
 → WindowsCaptureService.Crop
 → Crop Preview
```

## 7. 기존 기능 보호
수정 후 아래 기능이 깨지면 안 된다.

- Window 목록 조회
- Target 추가/삭제/활성화
- Background Capture
- Offline Image
- AOI Picker
- Crop / Crop Preview
- Workspace Load/Save

Legacy ON에서는 기존 Macro/Monitoring UI도 접근 가능해야 한다.

## 8. 빌드 및 검증
Solution 전체를 빌드하고 컴파일 오류는 0이어야 한다.

- [ ] 프로그램 실행
- [ ] Window Title = GameScope AI
- [ ] 상단 제품명 = GameScope AI
- [ ] Legacy 기본 OFF
- [ ] Legacy OFF에서 Macro/Monitoring/Trigger Action UI 숨김
- [ ] Legacy ON에서 기존 UI 다시 표시
- [ ] Offline Image 정상
- [ ] 이미지 Load 정상
- [ ] AOI 선택 정상
- [ ] Crop Preview 정상
- [ ] Window Target 기능 정상

## 금지 사항
- 기존 Automation 코드 삭제
- Macro/Input Service 삭제
- OCR 구현
- EXP/Level 인식 구현
- Local LLM 구현
- 대규모 MVVM 리팩터링
- 프로젝트/namespace 전체 이름 변경
- 새 이미지 처리 엔진 작성
- TASK-003 선행

## 완료 보고 형식

```text
TASK-002 RESULT

Modified Files:
- ...

Implemented:
- ...

Legacy UI:
- 기본 상태:
- 표시 방법:

Build:
- Result:
- Errors:
- Warnings:

Manual Test:
- GameScope Title:
- Legacy OFF:
- Legacy ON:
- Offline Image:
- AOI:
- Crop Preview:

Notes:
- ...

TASK-002 STATUS:
PASS / FAIL
```

## Git
현재 브랜치:

```text
feature/task-002-game-window-capture
```

Issue: `#1`

임의로 PR 생성, Merge, Issue Close를 수행하지 않는다. 사용자 검증 후 별도로 진행한다.
