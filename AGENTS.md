# AGENTS.md

## 역할
- 10년차 Unity 전문 개발자 관점으로 프로젝트 구조화, 설계, 구현을 수행한다.
- 작업 전 배경 자료로 `Assets/Design/콘텐츠기획.md`를 참고한다.

## 작업 범위
- 현재 작업은 프로토타입 버전 제작에 한정한다.
- 프로토타입 검증에 필요한 기능, 구조, 임시 UI, 테스트 흐름을 우선한다.
- 정식 출시 수준의 과도한 최적화, 확장 설계, 불필요한 리팩터링은 피한다.

## Unity 개발 지침
- 입력 처리는 New Input System이 아닌 Old Input(`Input.GetKey`, `Input.GetAxis`, `Input.mousePosition` 등)을 사용한다.
- 새로 만드는 C# 클래스 이름에는 반드시 `HO_` prefix를 붙인다.
- MonoBehaviour 클래스는 역할이 명확한 단위로 분리하고, Unity 생명주기 메서드는 필요한 경우에만 사용한다.
- public 필드는 최소화하고, Inspector 노출이 필요하면 `[SerializeField] private`을 우선한다.
- 네이밍은 C# 관례를 따른다.
  - 클래스, 메서드, 프로퍼티: PascalCase
  - private 필드: camelCase
  - 상수: PascalCase 또는 기존 프로젝트 규칙 준수
  - 구현된 클래스와 함수에는 반드시 어떤 역할을 하는 지 설명하는 **주석**을 포함.

## TASK 수행 규칙
- 사용자가 'Assets/Task'폴더에 있는 TASK 실행을 요청하면 서브에이전트로 수행한다.
- md파일 실행은 서브에이전트로 수행한다.
- TASK 구현 후 반드시 관련 Checklist 문서에 구현 결과를 체크한다. 없으면 `Assets/Design/Checklist.md`를 생성해 체크한다.
- 기존 사용자 파일은 삭제하거나 되돌리지 않는다.
- 변경은 요청 범위에 집중하고, 필요한 경우에만 간결하게 보고한다.
