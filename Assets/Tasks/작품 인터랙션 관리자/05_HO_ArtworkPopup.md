# Task 05. HO_ArtworkPopup

## 목적
작품 선택 결과를 표시할 공용 팝업을 먼저 만든다. 이후 선택기, 큐레이터, 복귀 컨트롤러가 같은 팝업 구조를 재사용한다.

## 생성할 GameObject 및 컴포넌트
- `Canvas_ArtworkPopup`
- `ArtworkPopupPanel`
- `ArtworkTitleText`
- `ArtworkMetaText`
- `ArtworkKeywordText`
- `ArtworkDescriptionText`
- `CloseButton`
- `HO_ArtworkPopup`

## SerializedField 및 Inspector 연결
- `[SerializeField] private GameObject popupRoot`
- `[SerializeField] private TMPro.TMP_Text titleText`
- `[SerializeField] private TMPro.TMP_Text metaText`
- `[SerializeField] private TMPro.TMP_Text keywordText`
- `[SerializeField] private TMPro.TMP_Text descriptionText`
- `[SerializeField] private Button closeButton`
- `popupRoot`에는 `ArtworkPopupPanel`, 나머지 필드는 대응 UI 컴포넌트를 연결한다.

## 구현 단계
1. 작품 정보 표시용 Canvas와 팝업 패널을 만든다.
2. `HO_ArtworkPopup` 클래스를 생성하고 열기/닫기 역할 주석을 작성한다.
3. 작품명, 매체, 키워드, 해설을 받는 표시 메서드를 만든다.
4. 팝업 열기 시 텍스트 갱신과 패널 활성화를 한 번에 처리한다.
5. 닫기 버튼과 외부 호출용 닫기 메서드를 모두 제공한다.
6. 프로토타입에서는 Inspector 배열 또는 단순 데이터 전달만 받도록 유지한다.

## 완료 조건
- 작품 정보 표시 로직이 `HO_ArtworkPopup` 한 클래스에만 모여 있다.
- 팝업 열기, 내용 갱신, 닫기가 모두 된다.
- 1920x1080 기준으로 텍스트 슬롯이 구분되어 있다.
- 다른 시스템이 메서드 호출만으로 팝업을 열 수 있다.
