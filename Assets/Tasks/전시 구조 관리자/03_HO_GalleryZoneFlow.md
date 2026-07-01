# Task 03. HO_GalleryZoneFlow

## 목적
로비부터 리플렉션 라운지까지의 구역 진입 상태를 관리하는 전시 동선 기준 클래스를 먼저 만든다. 이후 UI, 조명, 큐레이터 시스템이 이 상태를 참조한다.

## 생성할 GameObject 및 컴포넌트
- `GalleryZoneFlow`
- `ZoneTrigger_Lobby`
- `ZoneTrigger_Hallway`
- `ZoneTrigger_AbstractRoom`
- `ZoneTrigger_MediaRoom`
- `ZoneTrigger_CuratorZone`
- `ZoneTrigger_Lounge`
- `HO_GalleryZoneFlow`

## SerializedField 및 Inspector 연결
- `[SerializeField] private Collider[] zoneTriggers`
- `[SerializeField] private string[] zoneIds`
- `[SerializeField] private string defaultZoneId`
- `[SerializeField] private Transform playerTransform`
- `zoneTriggers`에는 각 공간의 Trigger Collider를 순서대로 연결한다.
- `playerTransform`에는 `Player`를 연결한다.

## 구현 단계
1. 공간별 Trigger Collider 오브젝트를 만들고 `Is Trigger`를 켠다.
2. `HO_GalleryZoneFlow` 클래스를 생성하고 구역 전환 역할 주석을 작성한다.
3. 현재 구역 ID와 다음 구역 ID를 저장할 구조를 만든다.
4. 플레이어가 Trigger에 진입하면 현재 구역 ID를 갱신한다.
5. 구역 변경 시 다음 구역 안내용 상태 신호를 외부에서 읽을 수 있게 노출한다.
6. 역주행 또는 재진입 상황에서도 마지막 유효 구역 상태가 유지되도록 예외 처리를 넣는다.

## 완료 조건
- 6개 전시 구역을 하나의 흐름 클래스로 관리한다.
- 구역 상태를 다른 클래스가 참조할 수 있다.
- Trigger 기반 전환 구조가 정리되어 있다.
- 프로토타입 기준으로 고정된 구역 순서를 사용한다.
