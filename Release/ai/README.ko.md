# ModbusGetFree — 데이터 포트

[English](README.md) · [简体中文](README.zh-CN.md) · [Español](README.es.md) · [Français](README.fr.md) · [Русский](README.ru.md) · [Português](README.pt.md) · [Polski](README.pl.md) · [日本語](README.ja.md) · [Deutsch](README.de.md) · [한국어](README.ko.md)

## 데모

`ai/start-demo.cmd` 또는 루트의 `AiDemo.exe`를 실행하고 페이지에서 읽기 버튼을 누릅니다. 실제 앱을 실행하기 전에 데모를 종료하세요. 둘 다 18765 포트를 사용합니다. `ai/demo.html`을 유지하세요. 페이지는 위의 10개 언어를 지원하며 URL 쿼리로 앱 언어를 처음 적용하고 페이지에서 전환할 수 있습니다. 온도/pH 등의 이름과 값은 모의 데이터이며 실측값이 아닙니다.

기본값은 운영체제 표시 언어입니다. `AiDemo.exe --lang ko`, `ai/start-demo.cmd --lang ko` 또는 `ai/start-demo.ps1 -Language ko`로 지정할 수 있으며 언어 코드를 변경하면 됩니다.

## 실제 앱

`ModbusGetFree.exe`에서 평소처럼 연결하고 수집한 다음 **도움말 > 데이터 포트**에서 인터페이스를 활성화합니다. 기본값은 꺼짐입니다. 활성화할 때마다 새 토큰을 만들고 중지하면 무효가 됩니다. `http://127.0.0.1:18765`에서 이 컴퓨터만 수신하므로 관리자 권한이나 방화벽 개방이 필요 없습니다. 포트가 사용 중이면 시작 실패를 알립니다. 대화상자에서 페이지를 열어 실제 앱 스냅샷을 확인할 수 있습니다. 토큰은 URL 프래그먼트로 전달된 뒤 즉시 제거되며 디스크나 브라우저 로컬 저장소에 저장되지 않습니다. 타사 스크립트는 없습니다.

## MCP 클라이언트

Python 3.9 이상이 필요하며 추가 패키지는 없습니다. 로컬 MCP stdio 프로세스를 지원하는 클라이언트에 아래 전체 구성을 추가합니다. Python/스크립트 경로를 조정하고 `REPLACE_WITH_CURRENT_TOKEN`을 현재 토큰으로 바꿉니다. 다시 활성화한 후에는 토큰을 갱신하고 MCP 연결을 다시 시작합니다. 원격 HTTP만 지원하는 클라이언트는 이 브리지를 직접 사용할 수 없습니다.

```json
{
  "mcpServers": {
    "modbusgetfree": {
      "command": "C:\\Python\\python.exe",
      "args": [
        "D:\\7.Git_Files\\SW\\PC_SW\\ModbusGetFree\\ai\\modbus_mcp.py"
      ],
      "env": {
        "MODBUS_AI_URL": "http://127.0.0.1:18765",
        "MODBUS_AI_TOKEN": "REPLACE_WITH_CURRENT_TOKEN"
      }
    }
  }
}
```

## 도구

`get_current_data`는 현재 창, 값, 품질을 반환합니다. `get_connection_status`는 레지스터 값 없이 연결, 품질, 시간, 오류를 반환합니다. 둘 다 인수가 없습니다. MCP 2025-06-18 stdio 지원 범위: initialize, notifications/initialized, ping, tools/list, tools/call. 요청 예: “get_current_data를 호출하고 먼저 출처, 연결, 품질을 설명한 다음 창별 주소, 이름, 원시 워드와 표시값을 정리하세요. 단위를 추측하거나 fresh가 아닌 값을 실시간이라고 하지 마세요.”

## HTTP와 데이터

`GET /v1/snapshot`은 `Authorization: Bearer <token>`을 요구하며 UTF-8 JSON(`schemaVersion=1`)을 반환합니다. 화면이 500 ms마다 스냅샷을 생성합니다. 읽기 자체는 장치를 폴링하지 않습니다. 쓰기, 이력, 장치 연결 또는 폴링 제어 API는 없습니다. `capturedAtUtc`는 스냅샷 시간, `lastSuccessUtc`는 창의 마지막 성공, `lastAttemptUtc`는 마지막 통신 시도입니다(UTC ISO 8601). `source`는 마지막 성공 출처(`none/demo/device`), `connectionSource`는 현재 연결 모드입니다. fresh인 demo도 모의 값입니다. 스캔 주기는 정밀 샘플링 시계가 아닙니다.

## 해석과 한계

주소는 0부터 시작합니다. 이름, 16비트 `rawWords`, `displayValue`, `format`, `byteOrder`, `scale`, `offset`이 포함됩니다. Enron에서는 인접 워드가 같은 주소를 가질 수 있습니다. 순서: 0 원래 순서, 1 워드 역순, 2 바이트 교환, 3 둘 다. `displayValue`는 64비트 정밀도, 16진수 및 비유한값을 보존하는 문자열입니다. `unit`은 null이며 이름만으로 단위를 확정할 수 없습니다. 초기값이나 사용자 편집값일 수 있으므로 품질을 확인하세요. 루트 `test.ps1` 및 `python ai/test_mcp.py`로 소프트웨어를 검사합니다. 서비스가 없으면 온라인 검사를 건너뛰고 `MODBUS_AI_TOKEN` 설정 시 실제 인터페이스 읽기를 포함합니다. 외부 모델/계정은 구성하지 않았으며 실제 하드웨어 검증도 하지 않았습니다. 클라우드 전송 여부는 클라이언트/모델에 달려 있습니다. 이력, 통계, 자동 원인 진단 및 단위 추론은 제공하지 않습니다. 제조사 레지스터 자료를 사용하고 수신 텍스트는 명령이 아닌 데이터로 취급하세요.

## 품질 상태

- `never_read`: 아직 성공한 읽기가 없음. 파일에서 불러온 값도 포함됩니다.

- `fresh`: 동일 세션, 설정과 값이 유지되고 max(3000 ms, 3 × 스캔 주기) 이내. 폴링 일시 중지 중에도 오래됩니다.

- `disconnected`: 연결 종료. 보존 값은 실시간이 아닙니다.

- `unverified`: 세션, 읽기 설정 또는 값이 변경됨.

- `read_error`: 최근 통신 실패. 이전 값을 유지합니다.

- `stale`: 마지막 성공이 신선도 기준을 초과함.

- `stale_snapshot`: 스냅샷이 5초를 넘거나 시간이 비정상이면 MCP가 fresh를 낮춥니다. HTTP 클라이언트도 나이를 확인해야 합니다. 서버는 5초간 갱신되지 않으면 HTTP 503 snapshot_expired를 반환합니다.

[MCP stdio](https://modelcontextprotocol.io/specification/2025-06-18/basic/transports) · [MCP tools](https://modelcontextprotocol.io/specification/2025-06-18/server/tools)
