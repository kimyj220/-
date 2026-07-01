# Checklist 11. HO_ViewReturnController

## 검증 항목
- [x] `HO_ViewReturnController`가 팝업, 자막, 선택 상태 정리를 받을 참조를 Inspector에서 연결하도록 설계되어 있다.
- [x] 닫기 대상 UI 우선순위가 코드 구조로 정리되어 있다.
- [x] `Input.GetKeyDown(KeyCode.Escape)`를 쓰는 Old Input 기준이 문서와 코드에 반영되어 있다.
- [x] 복귀 처리 역할 주석 작성 기준이 코드에 반영되어 있다.
- [ ] 미검증: ESC 입력 시 가장 최근 우선순위의 UI부터 닫힌다.
- [ ] 미검증: 닫기 버튼과 ESC가 같은 복귀 메서드를 사용한다.
- [ ] 미검증: 복귀 후 이동/시점 조작과 선택 상태가 정상으로 돌아온다.
