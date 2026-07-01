# Task 10. HO_CuratorDocent

## 목적
현재 구역과 선택된 작품에 맞는 도슨트 문장을 골라 `HO_CuratorSubtitle`로 전달하는 큐레이터 로직 클래스를 구현한다.

## 생성할 GameObject 및 컴포넌트
- `CuratorDocentSystem`
- `HO_CuratorDocent`

## SerializedField 및 Inspector 연결
- `[SerializeField] private HO_GalleryZoneFlow galleryZoneFlow`
- `[SerializeField] private HO_CuratorSubtitle curatorSubtitle`
- `[SerializeField] private string defaultDocentLine`
- `[SerializeField] private ScriptEntry[] zoneScriptEntries`
- `[SerializeField] private ScriptEntry[] artworkScriptEntries`
- `galleryZoneFlow`와 `curatorSubtitle`는 기존 시스템 오브젝트를 연결한다.

## 구현 단계
1. 구역별 기본 안내 문장과 작품별 상세 문장 데이터를 정리한다.
2. `HO_CuratorDocent` 클래스를 생성하고 문장 선택 기준 주석을 작성한다.
3. 현재 구역 ID 기준 기본 문장 조회 메서드를 만든다.
4. 작품 ID가 전달되면 작품 우선 문장을 선택하고, 없으면 구역 문장을 사용한다.
5. 빈 데이터일 때는 `defaultDocentLine`을 반환하도록 예외 처리를 넣는다.
6. 최종 문장을 `HO_CuratorSubtitle` 시작 메서드로 전달한다.

## 완료 조건
- 도슨트 문장 선택 로직이 `HO_CuratorDocent` 한 클래스에만 모여 있다.
- 구역 ID와 작품 ID를 Inspector 데이터로 연결할 수 있다.
- 자막 출력 자체는 `HO_CuratorSubtitle`에 맡기고, 이 클래스는 선택과 전달만 담당한다.
- 기본 문장 fallback이 준비되어 있다.
