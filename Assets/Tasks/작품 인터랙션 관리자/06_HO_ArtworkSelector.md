# Task 06. HO_ArtworkSelector

## 목적
플레이어 시점에서 작품을 클릭해 선택하고 `HO_ArtworkPopup`으로 상세 정보를 넘기는 선택 인터랙션을 구현한다.

## 생성할 GameObject 및 컴포넌트
- `ArtworkInteractionSystem`
- `HO_ArtworkSelector`
- `Artwork_*` 대상 오브젝트들
- `Collider` 또는 선택용 Collider

## SerializedField 및 Inspector 연결
- `[SerializeField] private Camera targetCamera`
- `[SerializeField] private LayerMask artworkLayerMask`
- `[SerializeField] private float maxSelectDistance`
- `[SerializeField] private HO_ArtworkPopup artworkPopup`
- `[SerializeField] private string selectableTag` 또는 동등한 분류값
- `targetCamera`에는 `Main Camera`, `artworkPopup`에는 `HO_ArtworkPopup`를 연결한다.

## 구현 단계
1. 선택 가능한 작품 오브젝트에 Layer 또는 Tag를 정한다.
2. `HO_ArtworkSelector` 클래스를 생성하고 Raycast 판정 역할 주석을 작성한다.
3. `Input.GetMouseButtonDown(0)`과 `Input.mousePosition`으로 클릭 입력을 읽는다.
4. 카메라 기준 Raycast를 발사해 가장 가까운 작품만 판정한다.
5. 선택 성공 시 강조 처리와 팝업 호출을 함께 실행한다.
6. 선택 불가 오브젝트나 빈 공간 클릭 시 아무 동작도 하지 않도록 방어 로직을 넣는다.

## 완료 조건
- 작품 선택 로직이 `HO_ArtworkSelector` 한 클래스에만 모여 있다.
- Old Input 기반 클릭 판정만 사용한다.
- 선택 성공 시 `HO_ArtworkPopup` 호출까지 연결된다.
- Layer 또는 Tag 기준 필터링이 Inspector에서 조정 가능하다.
