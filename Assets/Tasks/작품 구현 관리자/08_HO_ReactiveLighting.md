# Task 08. HO_ReactiveLighting

## 목적
구역 진입, 작품 선택, 영상 재생 상태에 따라 조명과 발광값을 바꾸는 공간 연출 클래스를 구현한다. 선행 시스템의 상태를 받아 쓰므로 중후반에 배치한다.

## 생성할 GameObject 및 컴포넌트
- `LightingDirector`
- `Light` 목록
- 발광 머티리얼이 적용된 렌더러들
- `HO_ReactiveLighting`

## SerializedField 및 Inspector 연결
- `[SerializeField] private HO_GalleryZoneFlow galleryZoneFlow`
- `[SerializeField] private HO_ArtworkSelector artworkSelector`
- `[SerializeField] private HO_MediaVideoPlayer[] mediaVideoPlayers`
- `[SerializeField] private Light[] targetLights`
- `[SerializeField] private Renderer[] emissiveRenderers`
- `[SerializeField] private float transitionDuration`
- 상태를 읽어올 참조들은 각각 대응 시스템 오브젝트에 연결한다.

## 구현 단계
1. 구역별로 분위기를 바꿀 Light와 발광 대상 렌더러를 정리한다.
2. `HO_ReactiveLighting` 클래스를 생성하고 조명 전환 역할 주석을 작성한다.
3. 로비, 복도, 추상 공간, 매체 공간 등 대표 상태 프리셋을 만든다.
4. `HO_GalleryZoneFlow` 상태를 기준으로 기본 조명 상태를 바꾼다.
5. 작품 선택 또는 영상 재생 시 보조 강조 연출을 덧입힌다.
6. 전환은 즉시 점프하지 말고 `transitionDuration` 내 보간으로 처리한다.

## 완료 조건
- 조명/발광 연출 로직이 `HO_ReactiveLighting` 한 클래스에만 모여 있다.
- 구역 상태와 작품 상태를 모두 반영할 수 있다.
- 실시간 조명 남용 없이 제한된 대상만 제어한다.
- 전환 시간과 대상 목록을 Inspector에서 조정 가능하다.
