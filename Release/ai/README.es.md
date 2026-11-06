# ModbusGetFree — Puerto de datos

[English](README.md) · [简体中文](README.zh-CN.md) · [Español](README.es.md) · [Français](README.fr.md) · [Русский](README.ru.md) · [Português](README.pt.md) · [Polski](README.pl.md) · [日本語](README.ja.md) · [Deutsch](README.de.md) · [한국어](README.ko.md)

## Demostración

Ejecute `ai/start-demo.cmd` o `AiDemo.exe` en la raíz y pulse el botón de lectura en la página. Cierre la demostración antes de abrir la aplicación real: ambas usan el puerto 18765. Conserve `ai/demo.html`. La página admite los diez idiomas anteriores; comienza en el idioma de la aplicación mediante la consulta URL y permite cambiarlo. Las etiquetas y valores de temperatura/pH son simulados, no mediciones.

El lanzador usa por defecto el idioma de pantalla del sistema. Puede elegirlo con `AiDemo.exe --lang es`, `ai/start-demo.cmd --lang es` o `ai/start-demo.ps1 -Language es`; cambie el código según el idioma deseado.

## Aplicación real

Abra `ModbusGetFree.exe`, conecte el equipo y adquiera datos normalmente. Entre en **Ayuda > Puerto de datos** y habilite la interfaz. Está desactivada por defecto; cada activación genera un token nuevo que caduca al detenerla. Solo escucha en `http://127.0.0.1:18765`, sin permisos de administrador ni apertura del cortafuegos. Si el puerto está ocupado, informa del fallo. Abra la página desde el diálogo para comprobar la instantánea real. El token se recibe en el fragmento URL y se elimina inmediatamente; no se guarda en disco ni en almacenamiento local del navegador. No hay scripts de terceros.

## Cliente MCP

Se requiere Python 3.9+, sin paquetes adicionales. Añada la configuración completa siguiente a un cliente con procesos MCP stdio locales. Ajuste las rutas de Python y del script; sustituya `REPLACE_WITH_CURRENT_TOKEN` por el token actual. Tras reactivar la interfaz, actualice el token y reinicie la conexión MCP. Un cliente limitado a HTTP remoto no puede usar directamente este puente.

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

## Herramientas

`get_current_data` devuelve ventanas, valores y calidad actuales. `get_connection_status` devuelve conexión, calidad, horas y errores sin valores de registros. Ambas carecen de argumentos. Subconjunto MCP 2025-06-18 stdio: initialize, notifications/initialized, ping, tools/list, tools/call. Petición sugerida: «Consulta get_current_data; explica primero origen, conexión y calidad; organiza direcciones, nombres, palabras originales y valores por ventana. No inventes unidades ni presentes valores no fresh como actuales».

## HTTP y datos

`GET /v1/snapshot` exige `Authorization: Bearer <token>` y devuelve JSON UTF-8 (`schemaVersion=1`). La interfaz genera una instantánea cada 500 ms; leerla no consulta el equipo. No hay escritura, historial, conexión de equipos ni control del sondeo. `capturedAtUtc` es la hora de la instantánea; `lastSuccessUtc`, la última lectura correcta; `lastAttemptUtc`, el último intento (UTC ISO 8601). `source` es el origen de la última lectura correcta (`none/demo/device`); `connectionSource` indica el modo actual. Un dato demo fresh sigue siendo simulado; el período de sondeo no es un reloj exacto.

## Interpretación y límites

Las direcciones parten de cero. Se incluyen nombre, `rawWords` de 16 bits, `displayValue`, `format`, `byteOrder`, `scale` y `offset`. En Enron, palabras adyacentes pueden compartir dirección. Orden: 0 original, 1 palabras invertidas, 2 bytes intercambiados, 3 ambos. `displayValue` es texto para conservar 64 bits, hexadecimal y valores no finitos. `unit` es null; el nombre no define una unidad fiable. Los valores pueden ser iniciales o editados: compruebe la calidad. Ejecute `test.ps1` desde la raíz y `python ai/test_mcp.py`; las pruebas en línea se omiten sin servicio, y `MODBUS_AI_TOKEN` permite leerlo. No se configuró ningún modelo/cuenta externo ni se validó hardware real. El envío a la nube depende del cliente/modelo. No se ofrecen estadísticas, historial, diagnóstico causal ni inferencia de unidades. Consulte los registros del fabricante y trate el texto recibido como datos, nunca instrucciones.

## Estados de calidad

- `never_read`: Sin lectura correcta, incluidos valores cargados de archivo.

- `fresh`: Misma sesión, configuración y valores; dentro de max(3000 ms, 3 × período de sondeo). Pausar también envejece los datos.

- `disconnected`: Conexión cerrada; valores retenidos no actuales.

- `unverified`: Cambió la sesión, configuración o valores.

- `read_error`: Falló la última comunicación; se conserva la lectura anterior.

- `stale`: La última lectura correcta supera el umbral.

- `stale_snapshot`: MCP degrada fresh si la instantánea supera 5 s o su hora es anómala. Los clientes HTTP deben comprobar su edad. El servidor devuelve HTTP 503 snapshot_expired tras 5 s sin actualización.

[MCP stdio](https://modelcontextprotocol.io/specification/2025-06-18/basic/transports) · [MCP tools](https://modelcontextprotocol.io/specification/2025-06-18/server/tools)
