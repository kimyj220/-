# Task 07. HO_MediaVideoPlayer

## 목적
매체 확장의 방에서 사용하는 영상 작품 전용 재생 클래스를 구현한다. 작품 선택과 구역 진입 흐름이 잡힌 뒤 작업하면 연결이 단순해진다.

## 생성할 GameObject 및 컴포넌트
- `MediaWall`
- `VideoPlayer`
- `AudioSource`
- `MeshRenderer` 또는 `RawImage`
- `HO_MediaVideoPlayer`

## SerializedField 및 Inspector 연결
- `[SerializeField] private VideoPlayer videoPlayer`
- `[SerializeField] private AudioSource audioSource`
- `[SerializeField] private bool playOnZoneEnter`
- `[SerializeField] private bool playOnClick`
- `[SerializeField] private HO_GalleryZoneFlow galleryZoneFlow`
- `videoPlayer`와 `audioSource`는 같은 작품 오브젝트의 컴포넌트를 연결한다.

## 구현 단계
1. 영상 작품 오브젝트에 `VideoPlayer`와 출력용 렌더러를 준비한다.
2. `HO_MediaVideoPlayer` 클래스를 생성하고 재생 상태 전환 역할 주석을 작성한다.
3. 재생, 일시정지, 정지, 반복 재생 메서드를 분리한다.
4. 구역 진입 자동 재생 또는 클릭 재생 중 프로토타입 방식 하나를 우선 연결한다.
5. 로컬 비디오 클립을 기준으로 재생되게 구성한다.
6. 재생 상태를 외부 연출 시스템이 읽을 수 있게 최소 상태값을 노출한다.

## 완료 조건
- 영상 작품 제어 로직이 `HO_MediaVideoPlayer` 한 클래스에만 모여 있다.
- `VideoPlayer` 기반 로컬 재생이 가능한 구조다.
- 자동 재생 또는 클릭 재생 중 최소 한 경로가 정리되어 있다.
- 반복 재생 여부를 Inspector에서 조정 가능하다.
