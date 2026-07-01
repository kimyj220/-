# Checklist 01. HO_PlayerMovement

## 검증 항목
- [x] `Player` 오브젝트에 `CharacterController`와 `HO_PlayerMovement`가 함께 배치되어 있다.
- [x] 이동 관련 노출값이 `[SerializeField] private` 기준으로 Inspector에 정리되어 있다.
- [x] `Horizontal`, `Vertical` 축 이름을 쓰는 Old Input 구조로 설계되어 있다.
- [x] 클래스와 주요 함수 역할 주석 작성 기준이 문서에 반영되어 있다.
- [ ] 미검증: WASD와 방향키 입력에 즉시 이동 반응이 나온다.
- [ ] 미검증: 벽 관통 없이 중력이 적용된다.
- [ ] 미검증: 이동 속도가 과도하지 않고 관람용 기본 속도로 느껴진다.
