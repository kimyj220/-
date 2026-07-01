# Task 01. HO_PlayerMovement

## 목적
1인칭 관람객이 갤러리 공간을 안정적으로 이동할 수 있도록 기본 이동 클래스를 구현한다. 프로토타입 범위에서는 CharacterController 기반 이동과 중력 처리만 담당한다.

## 생성할 GameObject 및 컴포넌트
- `Player`
- `CharacterController`
- `HO_PlayerMovement`

## SerializedField 및 Inspector 연결
- `[SerializeField] private float moveSpeed`
- `[SerializeField] private float gravity`
- `[SerializeField] private CharacterController characterController`
- `[SerializeField] private float groundSnapForce` 또는 동등한 지면 보정값
- `characterController`는 같은 `Player` 오브젝트의 `CharacterController`를 연결한다.

## 구현 단계
1. `Player` 오브젝트를 만들고 `CharacterController`를 추가한다.
2. `HO_PlayerMovement` 클래스를 생성하고 클래스/주요 함수에 역할 주석을 작성한다.
3. `Input.GetAxis("Horizontal")`, `Input.GetAxis("Vertical")`만 사용해 이동 벡터를 계산한다.
4. 카메라의 좌우 기준이 아닌 플레이어 로컬 축 기준으로 이동 방향을 정리한다.
5. 중력 누적값을 적용하고 `CharacterController.Move`로 프레임별 이동을 처리한다.
6. 입력이 없을 때 이동값이 자연스럽게 0으로 수렴하도록 정리한다.

## 완료 조건
- 이동 로직이 `HO_PlayerMovement` 한 클래스에만 모여 있다.
- Old Input만 사용한다.
- 이동 속도와 중력 값이 Inspector에서 조정 가능하다.
- 벽 충돌과 중력 처리를 `CharacterController` 기준으로 수행한다.
