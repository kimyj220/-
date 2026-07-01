# Task 04. HO_MiniRouteUI

## 목적
현재 구역과 다음 구역 방향을 화면에 간단히 보여주는 최소 동선 UI를 구현한다. 구역 흐름이 먼저 있어야 하므로 `HO_GalleryZoneFlow` 다음 순서로 작업한다.

## 생성할 GameObject 및 컴포넌트
- `Canvas_RouteUI`
- `RoutePanel`
- `ZoneLabel_Current`
- `ZoneLabel_Next`
- `HO_MiniRouteUI`

## SerializedField 및 Inspector 연결
- `[SerializeField] private HO_GalleryZoneFlow galleryZoneFlow`
- `[SerializeField] private GameObject[] zoneMarkers`
- `[SerializeField] private TMPro.TMP_Text currentZoneText`
- `[SerializeField] private TMPro.TMP_Text nextZoneText`
- `galleryZoneFlow`에는 `GalleryZoneFlow` 오브젝트의 `HO_GalleryZoneFlow`를 연결한다.

## 구현 단계
1. 화면 가장자리 기준의 작은 Route UI Canvas를 만든다.
2. `HO_MiniRouteUI` 클래스를 생성하고 현재 구역 표시 역할 주석을 작성한다.
3. 6개 구역 고정 순서에 맞는 텍스트 또는 아이콘 배열을 준비한다.
4. `HO_GalleryZoneFlow`의 현재 구역 상태를 읽어 현재/다음 구역 표시를 갱신한다.
5. 현재 구역은 강조, 다음 구역은 보조 표시로 분리한다.
6. 화면을 과하게 가리지 않도록 작은 크기와 짧은 텍스트로 정리한다.

## 완료 조건
- Route UI 갱신 로직이 `HO_MiniRouteUI` 한 클래스에만 모여 있다.
- `HO_GalleryZoneFlow` 참조만으로 현재/다음 구역이 계산된다.
- 6개 고정 구역 흐름이 UI에 반영된다.
- 텍스트나 아이콘 연결이 Inspector에서 끝난다.
