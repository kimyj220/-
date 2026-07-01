# Task 11. HO_ViewReturnController

## 목적
팝업, 자막, 확대 보기 등 관람 중 열린 상태를 ESC 또는 닫기 버튼으로 정리하고 기본 관람 상태로 복귀시키는 마지막 제어 클래스를 구현한다.

## 생성할 GameObject 및 컴포넌트
- `ViewReturnController`
- `HO_ViewReturnController`

## SerializedField 및 Inspector 연결
- `[SerializeField] private HO_ArtworkPopup artworkPopup`
- `[SerializeField] private HO_CuratorSubtitle curatorSubtitle`
- `[SerializeField] private HO_ArtworkSelector artworkSelector`
- `[SerializeField] private GameObject[] closablePanels`
- `[SerializeField] private MonoBehaviour[] inputLockTargets`
- 필요한 경우 닫기 버튼의 `OnClick`에도 복귀 메서드를 연결한다.

## 구현 단계
1. 현재 프로젝트에서 닫아야 하는 UI와 상태를 우선순위로 목록화한다.
2. `HO_ViewReturnController` 클래스를 생성하고 복귀 처리 역할 주석을 작성한다.
3. `Input.GetKeyDown(KeyCode.Escape)` 입력을 감지한다.
4. 가장 최근에 열린 UI부터 닫는 우선순위 로직을 만든다.
5. 팝업/자막 종료 후 선택 강조나 입력 잠금 상태를 해제한다.
6. 닫기 버튼에서도 같은 복귀 메서드를 호출하도록 연결한다.

## 완료 조건
- 복귀 제어 로직이 `HO_ViewReturnController` 한 클래스에만 모여 있다.
- Old Input의 `KeyCode.Escape`만 사용한다.
- 팝업과 자막을 공통 방식으로 닫을 수 있다.
- 복귀 후 기본 관람 상태로 돌아가기 위한 정리 로직이 포함된다.
