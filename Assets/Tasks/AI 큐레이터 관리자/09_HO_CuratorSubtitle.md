# Task 09. HO_CuratorSubtitle

## 목적
AI 큐레이터 대사를 순차 표시하고, 작품 근접 시 E키 유도 문구를 보여주는 자막 UI 클래스를 구현한다.

## 생성할 GameObject 및 컴포넌트
- `Canvas_CuratorSubtitle`
- `SubtitlePanel`
- `SubtitleText`
- `InteractionHintText`
- `HO_CuratorSubtitle`

## SerializedField 및 Inspector 연결
- `[SerializeField] private GameObject subtitleRoot`
- `[SerializeField] private TMPro.TMP_Text subtitleText`
- `[SerializeField] private TMPro.TMP_Text interactionHintText`
- `[SerializeField] private float subtitleDisplayTime`
- `[SerializeField] private float interactionHintDistance`
- `subtitleRoot`와 텍스트 필드는 같은 Canvas 하위 UI를 연결한다.

## 구현 단계
1. 하단 자막 패널과 E키 유도 텍스트를 포함한 Canvas를 만든다.
2. `HO_CuratorSubtitle` 클래스를 생성하고 대사 진행 역할 주석을 작성한다.
3. 자막 시작, 다음 문장 표시, 종료 메서드를 분리한다.
4. 고정 문자열 배열을 받아 순차 표시하는 프로토타입 구조로 구현한다.
5. `Input.GetKeyDown(KeyCode.E)` 입력으로 시작 가능한 경로를 준비한다.
6. 작품 중심 2미터 이내에서만 상호작용 힌트가 보이도록 거리 판정용 진입 메서드를 마련한다.

## 완료 조건
- 자막 표시 로직이 `HO_CuratorSubtitle` 한 클래스에만 모여 있다.
- Old Input의 `KeyCode.E`만 사용한다.
- 자막 패널과 상호작용 힌트가 분리되어 있다.
- 실제 LLM 없이 고정 대사 배열만으로 동작 가능한 구조다.
