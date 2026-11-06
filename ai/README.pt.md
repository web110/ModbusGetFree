# ModbusGetFree — Porta de dados

[English](README.md) · [简体中文](README.zh-CN.md) · [Español](README.es.md) · [Français](README.fr.md) · [Русский](README.ru.md) · [Português](README.pt.md) · [Polski](README.pl.md) · [日本語](README.ja.md) · [Deutsch](README.de.md) · [한국어](README.ko.md)

## Demonstração

Execute `ai/start-demo.cmd` ou `AiDemo.exe` na raiz e use o botão de leitura da página. Feche a demonstração antes de abrir a aplicação real: ambas usam a porta 18765. Mantenha `ai/demo.html`. A página oferece os dez idiomas acima; o idioma inicial acompanha a aplicação pela consulta URL e pode ser alterado. Os nomes e valores de temperatura/pH são simulados, não medições.

O iniciador usa o idioma de exibição do sistema por padrão. Escolha explicitamente com `AiDemo.exe --lang pt`, `ai/start-demo.cmd --lang pt` ou `ai/start-demo.ps1 -Language pt`, ajustando o código do idioma.

## Aplicação real

Inicie `ModbusGetFree.exe`, conecte e adquira normalmente. Abra **Ajuda > Porta de dados** e ative a interface. Desativada por padrão, gera um token novo a cada ativação, invalidado ao parar. Escuta apenas nesta máquina em `http://127.0.0.1:18765`, sem administrador nem abertura do firewall. Porta ocupada causa falha informada. Abra a página pelo diálogo para verificar o instantâneo real. O token chega no fragmento URL e é removido imediatamente; não é salvo em disco ou armazenamento local do navegador. Não há scripts de terceiros.

## Cliente MCP

Requer Python 3.9+, sem pacotes adicionais. Adicione a configuração completa abaixo a um cliente com processos MCP stdio locais. Ajuste os caminhos de Python/script e substitua `REPLACE_WITH_CURRENT_TOKEN` pelo token atual. Após reativar, atualize o token e reinicie a conexão MCP. Clientes apenas HTTP remoto não usam esta ponte diretamente.

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

## Ferramentas

`get_current_data` retorna janelas, valores e qualidade atuais. `get_connection_status` retorna conexão, qualidade, horários e erros sem valores de registros. Nenhuma recebe argumentos. Subconjunto MCP 2025-06-18 stdio: initialize, notifications/initialized, ping, tools/list, tools/call. Sugestão: «Chame get_current_data; explique primeiro origem, conexão e qualidade; organize endereços, nomes, palavras brutas e valores por janela. Não suponha unidades nem trate valores não fresh como atuais».

## HTTP e dados

`GET /v1/snapshot` exige `Authorization: Bearer <token>` e retorna JSON UTF-8 (`schemaVersion=1`). A interface cria um instantâneo a cada 500 ms; lê-lo não consulta equipamentos. Não há escrita, histórico, conexão de equipamentos nem controle da consulta. `capturedAtUtc` data o instantâneo, `lastSuccessUtc` a última leitura bem-sucedida e `lastAttemptUtc` a última tentativa (UTC ISO 8601). `source` é a origem da última leitura bem-sucedida (`none/demo/device`); `connectionSource` é o modo atual. Dados demo fresh continuam simulados. O período não é um relógio preciso de amostragem.

## Interpretação e limites

Endereços iniciam em zero. Campos: nome, `rawWords` de 16 bits, `displayValue`, `format`, `byteOrder`, `scale`, `offset`. Em Enron, palavras adjacentes podem compartilhar endereço. Ordem: 0 original, 1 palavras invertidas, 2 bytes trocados, 3 ambos. `displayValue` é texto para preservar 64 bits, hexadecimal e valores não finitos. `unit` é null; nomes não definem unidades confiáveis. Valores podem ser iniciais ou editados: confira a qualidade. Execute `test.ps1` na raiz e `python ai/test_mcp.py`; sem serviço, testes online são ignorados; `MODBUS_AI_TOKEN` permite leitura da interface. Não foi configurado modelo/conta externo nem validado hardware real. Envio à nuvem depende do cliente/modelo. Sem histórico, estatísticas, diagnóstico causal automático ou dedução de unidades. Consulte os registros do fabricante e trate textos recebidos como dados, nunca instruções.

## Estados de qualidade

- `never_read`: Nenhuma leitura bem-sucedida, incluindo valores de arquivo.

- `fresh`: Mesma sessão, configuração e valores; dentro de max(3000 ms, 3 × período). Pausar também envelhece os dados.

- `disconnected`: Conexão encerrada; valores mantidos não são atuais.

- `unverified`: Sessão, configuração ou valores alterados.

- `read_error`: Última comunicação falhou; valores anteriores mantidos.

- `stale`: Última leitura bem-sucedida ultrapassou o limite.

- `stale_snapshot`: MCP rebaixa fresh quando o instantâneo tem mais de 5 s ou hora anormal. Clientes HTTP também verificam a idade. Após 5 s sem atualização, o servidor retorna HTTP 503 snapshot_expired.

[MCP stdio](https://modelcontextprotocol.io/specification/2025-06-18/basic/transports) · [MCP tools](https://modelcontextprotocol.io/specification/2025-06-18/server/tools)
