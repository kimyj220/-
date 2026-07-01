# Checklist 06. HO_ArtworkSelector

## 검증 항목
- [x] 선택 가능한 작품에 Layer 또는 Tag 기준이 정의되어 있다.
- [x] `HO_ArtworkSelector`가 `Main Camera`와 `HO_ArtworkPopup` 참조를 Inspector에서 받도록 설계되어 있다.
- [x] `Input.GetMouseButtonDown(0)`과 `Input.mousePosition`을 쓰는 Old Input 기준이 문서에 반영되어 있다.
- [x] Raycast 판정 역할 주석 작성 기준이 문서에 반영되어 있다.
- [ ] 미검증: 작품 외 오브젝트 클릭 시 팝업이 열리지 않는다.
- [ ] 미검증: 가까운 작품만 선택된다.
- [ ] 미검증: 선택 성공 시 강조 표시와 팝업 호출이 함께 동작한다.
