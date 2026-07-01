# Task 02. HO_FirstPersonCamera

## 목적
플레이어 이동이 준비된 상태에서 1인칭 시점 회전을 추가해 갤러리 공간과 작품을 둘러볼 수 있게 한다.

## 생성할 GameObject 및 컴포넌트
- `PlayerCameraPivot`
- `Main Camera`
- `HO_FirstPersonCamera`

## SerializedField 및 Inspector 연결
- `[SerializeField] private Transform playerBody`
- `[SerializeField] private Transform cameraPivot`
- `[SerializeField] private float mouseSensitivity`
- `[SerializeField] private float minPitch`
- `[SerializeField] private float maxPitch`
- `playerBody`는 `Player`, `cameraPivot`은 카메라가 달린 피벗 오브젝트를 연결한다.

## 구현 단계
1. `Player` 하위에 `PlayerCameraPivot`과 `Main Camera`를 배치한다.
2. `HO_FirstPersonCamera` 클래스를 생성하고 회전축/제한 목적 주석을 작성한다.
3. `Input.GetAxis("Mouse X")`, `Input.GetAxis("Mouse Y")`만 사용해 마우스 입력을 읽는다.
4. 좌우 회전은 `playerBody`, 상하 회전은 `cameraPivot`에 분리 적용한다.
5. 상하 회전값은 `minPitch`, `maxPitch` 범위로 제한한다.
6. 기본 감도를 낮게 설정해 멀미를 줄이고, Inspector에서 조정 가능하게 한다.

## 완료 조건
- 시점 회전 로직이 `HO_FirstPersonCamera` 한 클래스에만 모여 있다.
- 좌우/상하 회전 축이 분리되어 있다.
- 상하 회전 제한이 적용된다.
- Old Input만 사용한다.
