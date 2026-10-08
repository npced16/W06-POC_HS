# 흑 · 잔향 — 1인칭 보스 결투 POC

어두운 건물 안에서 소리와 발자국으로 AI 보스 한 명을 추적하고, 짧은 잔향 감지와 위험한 받아치기로 빈틈을 만드는 액션 게임입니다.

검은 면 위에 얇은 흰 윤곽선만 그리는 산업 시설 스타일입니다. 배관, 볼트가 있는 플랜지, 문과 보스의 윤곽을 잔향 감지로 밝힙니다. 발자국과 검격도 흰 선으로 표현하며 HUD, Canvas, uGUI 메뉴는 없습니다.

## 실행

Unity 6000.0.55f1에서 `Assets/Scenes/WhistlePOC.unity`를 열고 Play를 누르면 바로 결투가 시작됩니다. `Whistle POC > Open prototype` 또는 `Open boss duel`도 같은 씬을 엽니다. 승패 후에는 Enter/R로 다시 도전합니다. 개발용 자원·체력은 BossDuel 프로퍼티로 확인하며, 피드백은 소리와 Console 로그 및 Feedback 이벤트로 전달합니다.

## 조작

| 입력 | 동작 |
|---|---|
| WASD | 자유 이동 |
| 마우스 | 1인칭 시점 |
| 좌클릭 | 베기: 기력 24, 사거리 2.7m, 행동 회복 0.72초 |
| 우클릭 | 받아치기: 기력 18, 성공 창 0.17초, 실패 시 행동 회복 0.8초 |
| Space | 잔향 감지: 숨 35, 시야 1.15초 |
| Left Shift | 이동 방향 회피, 입력이 없으면 뒤로 회피: 기력 30, 0.24초 |
| Esc | 일시정지 / 계속 |
| R | 결투 재시작 |
| Enter | 시작 / 계속 / 결과에서 재도전 |

## 전투

- 보스의 베기는 금속 마찰음, 내려찍기는 낮은 두 번의 울림, 돌진은 높아지는 세 번의 울림으로 예고합니다. 준비 후 마지막 0.35초에는 방향을 고정합니다.
- 보스는 문을 통해 방 사이를 이동합니다. 벽은 이동과 공격을 막습니다. 발자국은 지난 위치에 약 2.1초 남습니다.
- 보스 발소리와 공격음은 3D AudioSource로 재생합니다. 벽 너머에서는 음량을 낮추고 저역 통과 필터를 적용합니다. 별도 HRTF 플러그인은 사용하지 않습니다.
- 감지는 건물과 보스의 모습을 잠깐 밝힙니다. 숨은 감지 종료 후 초당 9 회복합니다. 큰 감지 소리는 보스를 교전으로 유도합니다.
- 일반 타격은 피해 1, 보스 공격 후 빈틈은 피해 3, 완벽한 받아치기 후 반격은 피해 5입니다. 받아치기 성공 시 보스가 1.6초 무방비가 되고 숨 30과 기력 25를 회복합니다.
- 보스 체력은 24입니다. 절반 이하에서는 이동과 공격 준비가 빨라집니다. 플레이어는 세 번 피격되면 패배합니다.

## 입력 구조

`BossAssets/DuelControls.inputactions`의 Duel 액션 맵에 Move, Look, Attack, Parry, Scan, Dodge, Pause, Restart, Confirm을 정의합니다.

`DuelInputSystem`은 에셋을 인스턴스별로 복제하고 Input System 콜백을 의미 단위 C# 이벤트로 발행합니다. `BossDuel` 매니저는 OnEnable에서 이벤트를 구독하고 OnDisable에서 해제합니다. 게임 매니저는 Keyboard/Mouse를 직접 폴링하지 않습니다. 키 바인딩은 Input Actions 에셋에서 수정할 수 있습니다.

## 소리 파동

플레이어·보스 발자국과 공격·충돌 소리가 발생하면 그 위치에서 흰 파동이 한 번 퍼집니다. 파동이 닿은 맵 윤곽만 잠깐 밝아졌다가 사라집니다. 소리 강도는 0~1이며 기본 범위는 `0.5 + 4 × 강도`미터입니다. 발걸음에는 별도로 2.5m 상한을 적용하므로 내 발소리는 약 1.4m, 보스 발소리는 2.5m까지 퍼집니다. 벽 뒤로는 맵을 밝히지 않으며 잔상은 0.04초 유지 후 0.2초 동안 사라집니다. `Sound Echo` 컴포넌트에서 최소·최대 범위, 전파 속도, 유지·감쇠 시간을 조절할 수 있습니다.

어떤 스크립트에서도 공통 이벤트를 발행할 수 있습니다. `SoundEcho`가 이를 구독해 표시하므로 게임 매니저를 참조할 필요가 없습니다.

```csharp
// 이미 재생한 소리의 파동만 발행
SoundEvents.Emit(transform.position, 0.5f);

// 재생과 파동을 함께 처리
SoundEvents.Play(audioSource, clip, transform.position, 0.8f);

// 특정 소리의 범위를 추가로 제한
SoundEvents.Play(audioSource, footstepClip, transform.position, 0.7f, radiusLimit: 2.5f);
```

이동 입력만 있고 벽에 막혀 실제로 움직이지 않으면 플레이어 발자국 소리와 파동을 만들지 않습니다. 파동은 HUD 없이 월드에 배치된 `Assets/Prepab/Sound Echo Wave.prefab`을 재사용합니다.

보스의 남은 체력은 몸에 붙은 흰 갑옷 표식 6개로 표시합니다. 가까운 교전 거리, 감지 중, 피격 직후에 표식이 나타나며 체력이 줄면 표식이 꺼집니다. 보스 사망 시 충격선과 함께 몸이 바닥으로 쓰러져 남고, 플레이어 사망 시 시점과 검이 바닥으로 떨어집니다. Canvas나 uGUI는 사용하지 않습니다.

## 씬과 검증

- 기본 보스전: `Assets/Scenes/WhistlePOC.unity`
- 기존 호위전 보관: `Assets/Scenes/WhistleEscort.unity`
- 기존 호위전 설명: `POCVerification/escort-prototype.md`
- 게임 진행 / 보스 / 자원 / 윤곽선: `Assets/Whistle/BossDuel.cs`
- 입력 이벤트: `Assets/Whistle/DuelInputSystem.cs`
- 씬과 에셋 생성: `Assets/Whistle/Editor/BossDuelAuthoring.cs`

Play Mode에서 `Whistle POC > Run boss duel smoke test`를 실행하면 입력 이벤트, 감지 자원, 보스 경로, 벽 충돌, 공격, 받아치기, 회피, 승패, 재시작과 Canvas/EventSystem 부재를 검사하고 `POCVerification/boss-duel-smoke.txt`에 기록합니다. 검사는 결투를 초기화합니다. 이전 호위전용 검사 메뉴는 보관된 호위전 씬에서 사용합니다.

`Rebuild boss duel`은 기본 씬의 배치를 다시 생성하는 개발용 메뉴입니다. 현재 씬을 저장하고 Play Mode를 종료한 뒤 사용합니다. 조형과 공격음은 기능 검증용 임시 에셋입니다.

## 아트 방향

- 첨부한 화풍 레퍼런스: `Docs/Art/monochrome-reference.png`
- built-in imagegen으로 생성한 개발 컨셉아트: `Docs/Art/boss-duel-concept-v1.png`
- 생성 프롬프트: `Docs/Art/boss-duel-concept-prompt.md`
- 실제 Unity 실행 화면: `POCVerification/boss-duel-monochrome.png`

컨셉아트는 산업 시설과 갑옷 보스의 목표 디자인입니다. 현재 실행 씬에는 기능 검증용 조형과 흑백 윤곽선 렌더링을 적용했습니다.
