# ModbusGetFree

[English](README.md) | [简体中文](README.zh-CN.md) | [Español](README.es.md) | [Français](README.fr.md) | [Русский](README.ru.md) | [Português](README.pt.md) | [Polski](README.pl.md) | [日本語](README.ja.md) | [Deutsch](README.de.md) | [한국어](README.ko.md)

Utilitário Modbus RTU para Windows, desenvolvido em C# / Windows Forms, tendo o Modbus Poll como referência.

Design: Jim NI. Copyright © 2026 Jim NI. Licença [MIT](LICENSE).

## Janela principal

![Janela principal em inglês](assets/screenshots/main-window-en.png)

A imagem apresenta dados de exemplo e a interface em inglês.

## Idiomas

O idioma predefinido é **inglês**. O menu **Language**, antes de Ajuda, permite escolher inglês, chinês simplificado, espanhol, francês, russo, português, polaco, japonês, alemão ou coreano. A seleção é guardada para o próximo arranque.

## Executar e compilar

Execute `ModbusGetFree.exe`. Para compilar, execute `./build.ps1` no PowerShell com o compilador do Windows .NET Framework. O código C# está em `src`.

## Funcionalidades

- Ligação série RTU, janelas de dados independentes e dispositivo escravo de demonstração sem hardware.
- Consulta periódica FC01/02/03/04 e operações FC05/06/15/16/17/22/23.
- O envio manual aguarda o fim da transação atual e suspende novas consultas na porta partilhada. As consultas são retomadas após a resposta, erro ou tempo limite. O temporizador continua ativo e as consultas perdidas não são acumuladas. As escritas por difusão não aguardam resposta.
- Escrita de bobinas e registos, máscaras AND/OR, leitura/escrita combinada e edição por tipo de dados.
- Formatos de apresentação, escala, tipos de letra, cores, gráficos, captura de tráfego e centro de testes.
- Documentos `.mpdoc`, espaços de trabalho `.mprtu` e importação de arquivos `.mbp` V7.x/V9.x compatíveis.

## Testes e documentação

`./test-ui-regression.ps1` verifica a interface e os idiomas; `./test.ps1` inclui os testes RTU. Os testes usam o dispositivo de demonstração e não escrevem no hardware. A ajuda está integrada. Consulte o [README em inglês](README.md) para detalhes sobre importação, recursos e testes.

## GitHub

Defina o URL HTTPS do projeto em `github-repository.txt` para a janela Sobre.
