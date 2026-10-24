using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ModbusGetFree
{
    public static partial class Language
    {
        public static readonly string[] Codes = { "en", "zh-CN", "es", "fr", "ru", "pt", "pl", "ja", "de", "ko" };
        public static readonly string[] Names = { "English", "简体中文", "Español", "Français", "Русский", "Português", "Polski", "日本語", "Deutsch", "한국어" };
        static string code = "en";
        public static string Code
        {
            get { return code; }
            set { code = Array.IndexOf(Codes, value) >= 0 ? value : "en"; }
        }
        public static bool Chinese { get { return Code == "zh-CN"; } set { Code = value ? "zh-CN" : "en"; } }
        static readonly Dictionary<string, string[]> additional = new Dictionary<string, string[]>();
        static readonly Dictionary<string, Dictionary<string, string>> catalogs = new Dictionary<string, Dictionary<string, string>>();
        static string Normalize(string text)
        {
            return Regex.Replace(text, @"\(&.\)", "").Replace("&", "").Trim().ToLowerInvariant();
        }
        public static string HelpText
        {
            get
            {
                return "ModbusGetFree\r\n\r\n" + string.Join("\r\n\r\n", new[] {
                    Text("Select a serial port, baud rate, parity and stop bits. Demo uses a simulated RTU slave."),
                    Text("F8: slave ID, function, address, quantity and scan rate. Addresses start at zero. F5 connects; F4 disconnects; F6 reads/writes once."),
                    Text("Right-click a value to set format, byte order, font, colors or scaling."),
                    Text("05/06 write one value; 15/16/23 support value lists; 22 provides AND/OR mask bits."),
                    Text("TC sends hexadecimal commands with optional checksums. Stopping traffic capture does not stop polling."),
                    Text("Save data as .mpdoc or workspaces as .mprtu. Compatible .mbp files can be imported."),
                    Text("Choose a language under Language. English is the default; your selection is saved."),
                    Text("Only one application can use a serial port at a time.") });
            }
        }
        static void InitializeAdditional()
        {
            foreach (string line in AdditionalCatalog.Split('\n'))
            {
                string[] cells = line.TrimEnd('\r').Split('|');
                if (cells.Length == 1 && cells[0].Length == 0) continue;
                if (cells.Length != 9) throw new InvalidOperationException("Invalid translation row: " + cells[0]);
                additional[Normalize(cells[0])] = cells;
            }
            for (int i = 2; i < Codes.Length; i++)
            {
                var translated = new Dictionary<string, string>();
                foreach (string key in zh.Keys)
                {
                    string result = TranslateAdditional(key, i - 1);
                    translated[key] = result;
                    if (!en.ContainsKey(result)) en[result] = key;
                }
                foreach (var row in additional.Values)
                    if (!en.ContainsKey(row[i - 1])) en[row[i - 1]] = row[0];
                catalogs[Codes[i]] = translated;
            }
        }
        static string TranslateAdditional(string text, int column)
        {
            string translated = TranslateAdditionalCore(text.Replace("&", ""), column);
            int marker = text.IndexOf('&');
            if (marker >= 0 && marker + 1 < text.Length && translated != text)
                translated += " (&" + char.ToUpperInvariant(text[marker + 1]) + ")";
            return translated;
        }
        static string TranslateAdditionalCore(string text, int column)
        {
            string[] row;
            if (additional.TryGetValue(Normalize(text), out row)) return row[column];
            if (text.EndsWith("...")) return TranslateAdditional(text.Substring(0, text.Length - 3), column) + "...";
            if (text.EndsWith(":")) return TranslateAdditional(text.Substring(0, text.Length - 1), column) + ":";
            Match function = Regex.Match(text.Replace("&", ""), @"^(\d{2})[: ]+(.+?)( \([0134]x\))?$");
            if (function.Success)
            {
                string caption = TranslateAdditional(function.Groups[2].Value, column);
                if (caption != function.Groups[2].Value)
                    return function.Groups[1].Value + ": " + caption + function.Groups[3].Value;
            }
            Match series = Regex.Match(text.Replace("&", ""), @"^Series (\d+)$");
            if (series.Success) return TranslateAdditional("Series", column) + " " + series.Groups[1].Value;
            foreach (string separator in new[] { " — ", "|", " = " })
                if (text.Contains(separator))
                {
                    string[] parts = text.Split(new[] { separator }, StringSplitOptions.None);
                    for (int i = 0; i < parts.Length; i++) parts[i] = TranslateAdditional(parts[i], column);
                    return string.Join(separator, parts);
                }
            // Numeric formats share type and byte-order vocabulary, without touching values.
            string format = Regex.Replace(text, @"^(Int|UInt|Float|Double)(32|64)? (.+)$", m =>
                TranslateAdditional(m.Groups[1].Value == "Int" ? "Signed" : m.Groups[1].Value == "UInt" ? "Unsigned" : m.Groups[1].Value, column)
                + " " + (m.Groups[2].Success ? m.Groups[2].Value : m.Groups[1].Value == "Float" ? "32" : "64") + " " + TranslateAdditional(m.Groups[3].Value, column));
            if (format != text) return format;
            Match bits = Regex.Match(text, @"^(Signed|Unsigned|Float|Double) (\d+)-bit$|^(\d+) Bit (signed|Unsigned|Float|Double)$", RegexOptions.IgnoreCase);
            if (bits.Success) return (bits.Groups[2].Success ? bits.Groups[2].Value : bits.Groups[3].Value) + " " + TranslateAdditional(bits.Groups[1].Success ? bits.Groups[1].Value : bits.Groups[4].Value, column);
            Match write = Regex.Match(text, @"^Write (unsigned int|int|float|double) (32|64)$");
            if (write.Success) return TranslateAdditional("Write", column) + " " + TranslateAdditional(write.Groups[1].Value == "int" ? "Signed" : write.Groups[1].Value == "unsigned int" ? "Unsigned" : write.Groups[1].Value, column) + " " + write.Groups[2].Value;
            return text;
        }
        const string AdditionalCatalog = @"
File|Archivo|Fichier|Файл|Ficheiro|Plik|ファイル|Datei|파일
New|Nuevo|Nouveau|Создать|Novo|Nowy|新規|Neu|새로 만들기
Open|Abrir|Ouvrir|Открыть|Abrir|Otwórz|開く|Öffnen|열기
Close|Cerrar|Fermer|Закрыть|Fechar|Zamknij|閉じる|Schließen|닫기
Save|Guardar|Enregistrer|Сохранить|Guardar|Zapisz|保存|Speichern|저장
Save As|Guardar como|Enregistrer sous|Сохранить как|Guardar como|Zapisz jako|名前を付けて保存|Speichern unter|다른 이름으로 저장
Open Workspace|Abrir espacio de trabajo|Ouvrir l’espace de travail|Открыть рабочую область|Abrir área de trabalho|Otwórz obszar roboczy|ワークスペースを開く|Arbeitsbereich öffnen|작업 영역 열기
Save Workspace|Guardar espacio de trabajo|Enregistrer l’espace de travail|Сохранить рабочую область|Guardar área de trabalho|Zapisz obszar roboczy|ワークスペースを保存|Arbeitsbereich speichern|작업 영역 저장
Print|Imprimir|Imprimer|Печать|Imprimir|Drukuj|印刷|Drucken|인쇄
Print Preview|Vista previa de impresión|Aperçu avant impression|Предварительный просмотр|Pré-visualizar impressão|Podgląd wydruku|印刷プレビュー|Druckvorschau|인쇄 미리 보기
Print Setup|Configurar impresión|Configuration de l’impression|Настройка печати|Configurar impressão|Ustawienia drukowania|印刷設定|Druckereinrichtung|인쇄 설정
Recent File|Archivos recientes|Fichiers récents|Недавние файлы|Ficheiros recentes|Ostatnie pliki|最近使ったファイル|Zuletzt verwendete Dateien|최근 파일
(No recent files)|(Sin archivos recientes)|(Aucun fichier récent)|(Нет недавних файлов)|(Sem ficheiros recentes)|(Brak ostatnich plików)|（最近使ったファイルなし）|(Keine zuletzt verwendeten Dateien)|(최근 파일 없음)
Exit|Salir|Quitter|Выход|Sair|Wyjdź|終了|Beenden|종료
Edit|Editar|Édition|Правка|Editar|Edycja|編集|Bearbeiten|편집
Cut|Cortar|Couper|Вырезать|Cortar|Wytnij|切り取り|Ausschneiden|잘라내기
Copy|Copiar|Copier|Копировать|Copiar|Kopiuj|コピー|Kopieren|복사
Paste|Pegar|Coller|Вставить|Colar|Wklej|貼り付け|Einfügen|붙여넣기
Select All|Seleccionar todo|Tout sélectionner|Выделить всё|Selecionar tudo|Zaznacz wszystko|すべて選択|Alles auswählen|모두 선택
Connection|Conexión|Connexion|Подключение|Ligação|Połączenie|接続|Verbindung|연결
Connect|Conectar|Connecter|Подключить|Ligar|Połącz|接続|Verbinden|연결
Disconnect|Desconectar|Déconnecter|Отключить|Desligar|Rozłącz|切断|Trennen|연결 해제
Auto Connect|Conexión automática|Connexion automatique|Автоподключение|Ligação automática|Automatyczne łączenie|自動接続|Automatisch verbinden|자동 연결
Enable|Activar|Activer|Включить|Ativar|Włącz|有効|Aktivieren|활성화
Disable|Desactivar|Désactiver|Отключить|Desativar|Wyłącz|無効|Deaktivieren|비활성화
Disabled|Desactivado|Désactivé|Отключено|Desativado|Wyłączone|無効|Deaktiviert|비활성화됨
Quick Connect|Conexión rápida|Connexion rapide|Быстрое подключение|Ligação rápida|Szybkie łączenie|クイック接続|Schnellverbindung|빠른 연결
Setup|Configuración|Configuration|Настройка|Configuração|Ustawienia|設定|Einrichtung|설정
Read/Write Definition|Definición de lectura/escritura|Définition lecture/écriture|Настройка чтения/записи|Definição de leitura/escrita|Definicja odczytu/zapisu|読み書き設定|Lese-/Schreibdefinition|읽기/쓰기 설정
Read/Write Once|Leer/escribir una vez|Lire/écrire une fois|Однократное чтение/запись|Ler/escrever uma vez|Jednorazowy odczyt/zapis|1回読み書き|Einmal lesen/schreiben|한 번 읽기/쓰기
Read/Write Disabled|Lectura/escritura desactivada|Lecture/écriture désactivée|Чтение/запись отключены|Leitura/escrita desativada|Odczyt/zapis wyłączony|読み書き無効|Lesen/Schreiben deaktiviert|읽기/쓰기 비활성화
Excel Log|Registro Excel|Journal Excel|Журнал Excel|Registo Excel|Dziennik Excel|Excel ログ|Excel-Protokoll|Excel 로그
Excel Logging Off|Detener registro Excel|Arrêter le journal Excel|Остановить журнал Excel|Parar registo Excel|Zatrzymaj dziennik Excel|Excel ログ停止|Excel-Protokoll beenden|Excel 로그 중지
Log|Registro|Journal|Журнал|Registo|Dziennik|ログ|Protokoll|로그
Logging Off|Detener registro|Arrêter le journal|Остановить журнал|Parar registo|Zatrzymaj dziennik|ログ停止|Protokollierung beenden|로그 중지
Log off|Detener registro|Arrêter le journal|Остановить журнал|Parar registo|Zatrzymaj dziennik|ログ停止|Protokollierung beenden|로그 중지
Reset Counters|Reiniciar contadores|Réinitialiser les compteurs|Сбросить счётчики|Repor contadores|Wyzeruj liczniki|カウンターをリセット|Zähler zurücksetzen|카운터 초기화
Reset All Counters|Reiniciar todos los contadores|Réinitialiser tous les compteurs|Сбросить все счётчики|Repor todos os contadores|Wyzeruj wszystkie liczniki|全カウンターをリセット|Alle Zähler zurücksetzen|모든 카운터 초기화
Use as Default|Usar como predeterminado|Utiliser par défaut|Использовать по умолчанию|Usar como predefinição|Ustaw jako domyślne|既定値に設定|Als Standard verwenden|기본값으로 설정
Functions|Funciones|Fonctions|Функции|Funções|Funkcje|機能|Funktionen|기능
Write Single Coil|Escribir bobina individual|Écrire une bobine|Записать одну катушку|Escrever uma bobina|Zapisz jedną cewkę|単一コイル書き込み|Einzelne Spule schreiben|단일 코일 쓰기
Write Single Register|Escribir registro individual|Écrire un registre|Записать один регистр|Escrever um registo|Zapisz jeden rejestr|単一レジスタ書き込み|Einzelnes Register schreiben|단일 레지스터 쓰기
Write Coils|Escribir bobinas|Écrire des bobines|Записать катушки|Escrever bobinas|Zapisz cewki|コイル書き込み|Spulen schreiben|코일 쓰기
Write Registers|Escribir registros|Écrire des registres|Записать регистры|Escrever registos|Zapisz rejestry|レジスタ書き込み|Register schreiben|레지스터 쓰기
Write Multiple Coils|Escribir varias bobinas|Écrire plusieurs bobines|Записать несколько катушек|Escrever várias bobinas|Zapisz wiele cewek|複数コイル書き込み|Mehrere Spulen schreiben|여러 코일 쓰기
Write Multiple Registers|Escribir varios registros|Écrire plusieurs registres|Записать несколько регистров|Escrever vários registos|Zapisz wiele rejestrów|複数レジスタ書き込み|Mehrere Register schreiben|여러 레지스터 쓰기
Report Slave ID|Consultar ID del esclavo|Lire l’identifiant de l’esclave|Получить ID ведомого|Consultar ID do escravo|Odczytaj ID urządzenia podrzędnego|スレーブ ID 取得|Slave-ID abfragen|슬레이브 ID 조회
Mask Write Register|Escribir registro con máscara|Écrire un registre avec masque|Записать регистр по маске|Escrever registo com máscara|Zapisz rejestr z maską|マスク付きレジスタ書き込み|Register mit Maske schreiben|마스크 레지스터 쓰기
Read/Write Registers|Leer/escribir registros|Lire/écrire des registres|Чтение/запись регистров|Ler/escrever registos|Odczyt/zapis rejestrów|レジスタ読み書き|Register lesen/schreiben|레지스터 읽기/쓰기
Read/Write Multiple Registers|Leer/escribir varios registros|Lire/écrire plusieurs registres|Чтение/запись нескольких регистров|Ler/escrever vários registos|Odczyt/zapis wielu rejestrów|複数レジスタ読み書き|Mehrere Register lesen/schreiben|여러 레지스터 읽기/쓰기
Test Center|Centro de pruebas|Centre de test|Центр тестирования|Centro de testes|Centrum testów|テストセンター|Testcenter|테스트 센터
Display|Visualización|Affichage|Отображение|Visualização|Wyświetlanie|表示形式|Darstellung|표시 형식
Signed|Con signo|Signé|Со знаком|Com sinal|Ze znakiem|符号付き整数|Mit Vorzeichen|부호 있는 정수
Unsigned|Sin signo|Non signé|Без знака|Sem sinal|Bez znaku|符号なし整数|Ohne Vorzeichen|부호 없는 정수
Hex - ASCII|Hexadecimal - ASCII|Hexadécimal - ASCII|Шестнадцатеричный - ASCII|Hexadecimal - ASCII|Szesnastkowy - ASCII|16進数 - ASCII|Hexadezimal - ASCII|16진수 - ASCII
Binary|Binario|Binaire|Двоичный|Binário|Binarny|2進数|Binär|2진수
Hex|Hexadecimal|Hexadécimal|Шестнадцатеричный|Hexadecimal|Szesnastkowy|16進数|Hexadezimal|16진수
Float|Coma flotante|Flottant|С плавающей точкой|Vírgula flutuante|Zmiennoprzecinkowy|浮動小数点数|Gleitkomma|부동 소수점
Double|Doble precisión|Double précision|Двойная точность|Dupla precisão|Podwójna precyzja|倍精度浮動小数点数|Doppelte Genauigkeit|배정밀도
Big-endian|Big-endian|Gros-boutiste|Старший байт первым|Big-endian|Big-endian|ビッグエンディアン|Big-Endian|빅 엔디언
Little-endian|Little-endian|Petit-boutiste|Младший байт первым|Little-endian|Little-endian|リトルエンディアン|Little-Endian|리틀 엔디언
Big-endian byte swap|Big-endian con bytes intercambiados|Gros-boutiste, octets permutés|Старший байт первым, обмен байтов|Big-endian com bytes trocados|Big-endian z zamianą bajtów|ビッグエンディアン・バイト交換|Big-Endian mit Byte-Tausch|빅 엔디언 바이트 교환
Little-endian byte swap|Little-endian con bytes intercambiados|Petit-boutiste, octets permutés|Младший байт первым, обмен байтов|Little-endian com bytes trocados|Little-endian z zamianą bajtów|リトルエンディアン・バイト交換|Little-Endian mit Byte-Tausch|리틀 엔디언 바이트 교환
Communication|Comunicación|Communication|Обмен данными|Comunicação|Komunikacja|通信記録|Kommunikation|통신
Real time Charting|Gráfico en tiempo real|Graphique en temps réel|График в реальном времени|Gráfico em tempo real|Wykres w czasie rzeczywistym|リアルタイムグラフ|Echtzeitdiagramm|실시간 차트
Link to Chart|Vincular al gráfico|Lier au graphique|Связать с графиком|Associar ao gráfico|Powiąż z wykresem|グラフに関連付け|Mit Diagramm verknüpfen|차트에 연결
Unlink|Desvincular|Délier|Удалить связь|Desassociar|Usuń powiązanie|関連付け解除|Verknüpfung lösen|연결 해제
Unlink All|Desvincular todo|Tout délier|Удалить все связи|Desassociar tudo|Usuń wszystkie powiązania|全関連付け解除|Alle Verknüpfungen lösen|모든 연결 해제
Colors|Colores|Couleurs|Цвета|Cores|Kolory|色|Farben|색상
Font|Fuente|Police|Шрифт|Tipo de letra|Czcionka|フォント|Schriftart|글꼴
Scaling|Escalado|Mise à l’échelle|Масштабирование|Escala|Skalowanie|数値換算|Skalierung|배율 변환
PLC Addresses (Base 1)|Direcciones PLC (base 1)|Adresses PLC (base 1)|Адреса PLC (с 1)|Endereços PLC (base 1)|Adresy PLC (od 1)|PLC アドレス（1始まり）|PLC-Adressen (ab 1)|PLC 주소 (1부터)
Protocol Addresses (Base 0)|Direcciones de protocolo (base 0)|Adresses du protocole (base 0)|Адреса протокола (с 0)|Endereços de protocolo (base 0)|Adresy protokołu (od 0)|プロトコルアドレス（0始まり）|Protokolladressen (ab 0)|프로토콜 주소 (0부터)
Error Counters|Contadores de errores|Compteurs d’erreurs|Счётчики ошибок|Contadores de erros|Liczniki błędów|エラーカウンター|Fehlerzähler|오류 카운터
View|Ver|Affichage|Вид|Ver|Widok|表示|Ansicht|보기
Toolbar|Barra de herramientas|Barre d’outils|Панель инструментов|Barra de ferramentas|Pasek narzędzi|ツールバー|Symbolleiste|도구 모음
Status Bar|Barra de estado|Barre d’état|Строка состояния|Barra de estado|Pasek stanu|ステータスバー|Statusleiste|상태 표시줄
Always On Top|Siempre visible|Toujours au premier plan|Поверх всех окон|Sempre visível|Zawsze na wierzchu|常に手前に表示|Immer im Vordergrund|항상 위에 표시
Window|Ventana|Fenêtre|Окно|Janela|Okno|ウィンドウ|Fenster|창
Cascade|Cascada|Cascade|Каскадом|Cascata|Kaskadowo|重ねて表示|Überlappend|계단식
Tile|Mosaico|Mosaïque|Рядом|Mosaico|Sąsiadująco|並べて表示|Nebeneinander|바둑판식
Arrange Icons|Organizar iconos|Réorganiser les icônes|Упорядочить значки|Organizar ícones|Rozmieść ikony|アイコンの整列|Symbole anordnen|아이콘 정렬
Help|Ayuda|Aide|Справка|Ajuda|Pomoc|ヘルプ|Hilfe|도움말
Help Topics|Temas de ayuda|Rubriques d’aide|Разделы справки|Tópicos de ajuda|Tematy pomocy|ヘルプトピック|Hilfethemen|도움말 항목
About ModbusGetFree|Acerca de ModbusGetFree|À propos de ModbusGetFree|О ModbusGetFree|Sobre o ModbusGetFree|O ModbusGetFree|ModbusGetFree について|Über ModbusGetFree|ModbusGetFree 정보
OK|Aceptar|OK|ОК|OK|OK|OK|OK|확인
Cancel|Cancelar|Annuler|Отмена|Cancelar|Anuluj|キャンセル|Abbrechen|취소
Apply|Aplicar|Appliquer|Применить|Aplicar|Zastosuj|適用|Übernehmen|적용
Send|Enviar|Envoyer|Отправить|Enviar|Wyślij|送信|Senden|전송
Clear|Limpiar|Effacer|Очистить|Limpar|Wyczyść|クリア|Leeren|지우기
Start|Iniciar|Démarrer|Начать|Iniciar|Uruchom|開始|Starten|시작
Stop|Detener|Arrêter|Остановить|Parar|Zatrzymaj|停止|Stoppen|중지
Value|Valor|Valeur|Значение|Valor|Wartość|値|Wert|값
Values|Valores|Valeurs|Значения|Valores|Wartości|値|Werte|값 목록
Result|Resultado|Résultat|Результат|Resultado|Wynik|結果|Ergebnis|결과
Address|Dirección|Adresse|Адрес|Endereço|Adres|アドレス|Adresse|주소
Name|Nombre|Nom|Имя|Nome|Nazwa|名前|Name|이름
Function|Función|Fonction|Функция|Função|Funkcja|機能コード|Funktion|기능 코드
Quantity|Cantidad|Quantité|Количество|Quantidade|Liczba|数量|Anzahl|수량
Slave ID|ID esclavo|ID esclave|ID ведомого|ID escravo|ID stacji|スレーブ ID|Slave-ID|슬레이브 ID
Scan Rate|Sondeo|Période|Период|Intervalo|Okres|スキャン間隔|Abfrageintervall|스캔 간격
Disable on error|Desactivar en caso de error|Désactiver en cas d’erreur|Отключить при ошибке|Desativar em caso de erro|Wyłącz przy błędzie|エラー時に無効化|Bei Fehler deaktivieren|오류 시 비활성화
Connection Setup|Configuración de conexión|Configuration de connexion|Настройка подключения|Configuração da ligação|Ustawienia połączenia|接続設定|Verbindung einrichten|연결 설정
Serial Port / Modbus RTU|Puerto serie / Modbus RTU|Port série / Modbus RTU|Последовательный порт / Modbus RTU|Porta série / Modbus RTU|Port szeregowy / Modbus RTU|シリアルポート / Modbus RTU|Serielle Schnittstelle / Modbus RTU|직렬 포트 / Modbus RTU
Serial port|Puerto serie|Port série|Последовательный порт|Porta série|Port szeregowy|シリアルポート|Serielle Schnittstelle|직렬 포트
Baud rate|Velocidad en baudios|Débit en bauds|Скорость передачи|Velocidade em baud|Prędkość transmisji|ボーレート|Baudrate|전송 속도
Data bits|Bits de datos|Bits de données|Биты данных|Bits de dados|Bity danych|データビット|Datenbits|데이터 비트
Parity|Paridad|Parité|Чётность|Paridade|Parzystość|パリティ|Parität|패리티
Stop bits|Bits de parada|Bits d’arrêt|Стоповые биты|Bits de paragem|Bity stopu|ストップビット|Stoppbits|정지 비트
Response timeout (ms)|Tiempo de espera de respuesta (ms)|Délai de réponse (ms)|Тайм-аут ответа (мс)|Tempo limite de resposta (ms)|Limit czasu odpowiedzi (ms)|応答タイムアウト（ms）|Antwortzeitlimit (ms)|응답 제한 시간 (ms)
Delay between polls (ms)|Pausa entre consultas (ms)|Délai entre les requêtes (ms)|Пауза между опросами (мс)|Atraso entre consultas (ms)|Odstęp między zapytaniami (ms)|ポーリング間隔（ms）|Pause zwischen Abfragen (ms)|폴링 간 지연 (ms)
RS-485 direction|Dirección RS-485|Direction RS-485|Направление RS-485|Direção RS-485|Kierunek RS-485|RS-485 方向制御|RS-485-Richtung|RS-485 방향
Control line|Línea de control|Ligne de commande|Линия управления|Linha de controlo|Linia sterująca|制御線|Steuerleitung|제어선
Adapter echo|Eco del adaptador|Écho de l’adaptateur|Эхо адаптера|Eco do adaptador|Echo adaptera|アダプターエコー|Adapterecho|어댑터 에코
Demo (no hardware)|Demo (sin hardware)|Démo (sans matériel)|Демо (без оборудования)|Demonstração (sem equipamento)|Demo (bez sprzętu)|デモ（機器不要）|Demo (ohne Hardware)|데모 (하드웨어 불필요)
RTS toggle|Conmutar RTS|Basculer RTS|Переключать RTS|Alternar RTS|Przełączaj RTS|RTS 切り替え|RTS umschalten|RTS 전환
DTR enable|Activar DTR|Activer DTR|Включить DTR|Ativar DTR|Włącz DTR|DTR 有効|DTR aktivieren|DTR 활성화
Remove local echo|Eliminar eco local|Supprimer l’écho local|Удалять локальное эхо|Remover eco local|Usuń lokalne echo|ローカルエコー除去|Lokales Echo entfernen|로컬 에코 제거
Use simulated RTU slave|Usar esclavo RTU simulado|Utiliser un esclave RTU simulé|Использовать эмулятор ведомого RTU|Usar escravo RTU simulado|Użyj symulowanego urządzenia RTU|模擬 RTU スレーブを使用|Simulierten RTU-Slave verwenden|모의 RTU 슬레이브 사용
None|Ninguna|Aucune|Нет|Nenhuma|Brak|なし|Keine|없음
Odd|Impar|Impaire|Нечётная|Ímpar|Nieparzysta|奇数|Ungerade|홀수
Even|Par|Paire|Чётная|Par|Parzysta|偶数|Gerade|짝수
Mark|Marca|Marque|Постоянная 1|Marca|Stała 1|マーク|Mark|마크
Space|Espacio|Espace|Постоянный 0|Espaço|Stałe 0|スペース|Space|스페이스
No Connection — press F3 to connect|Sin conexión — pulse F3 para conectar|Non connecté — F3 pour connecter|Нет подключения — нажмите F3|Sem ligação — prima F3 para ligar|Brak połączenia — naciśnij F3|未接続 — F3 で接続|Nicht verbunden — F3 zum Verbinden|연결 안 됨 — F3으로 연결
Connected — RTU Demo|Conectado — demo RTU|Connecté — démo RTU|Подключено — демо RTU|Ligado — demonstração RTU|Połączono — demo RTU|接続済み — RTU デモ|Verbunden — RTU-Demo|연결됨 — RTU 데모
No connection|Sin conexión|Non connecté|Нет подключения|Sem ligação|Brak połączenia|未接続|Nicht verbunden|연결 안 됨
Connection Error|Error de conexión|Erreur de connexion|Ошибка подключения|Erro de ligação|Błąd połączenia|接続エラー|Verbindungsfehler|연결 오류
Application Error|Error de aplicación|Erreur de l’application|Ошибка приложения|Erro da aplicação|Błąd aplikacji|アプリケーションエラー|Anwendungsfehler|프로그램 오류
Save Error|Error al guardar|Erreur d’enregistrement|Ошибка сохранения|Erro ao guardar|Błąd zapisu|保存エラー|Fehler beim Speichern|저장 오류
Open Error|Error al abrir|Erreur d’ouverture|Ошибка открытия|Erro ao abrir|Błąd otwierania|読み込みエラー|Fehler beim Öffnen|열기 오류
Workspace|Espacio de trabajo|Espace de travail|Рабочая область|Área de trabalho|Obszar roboczy|ワークスペース|Arbeitsbereich|작업 영역
Use Function|Usar función|Utiliser la fonction|Использовать функцию|Usar função|Użyj funkcji|機能コードを使用|Funktion verwenden|기능 코드 사용
On|Activado|Activé|Вкл.|Ligado|Wł.|オン|Ein|켜기
Off|Desactivado|Désactivé|Выкл.|Desligado|Wył.|オフ|Aus|끄기
Response ok|Respuesta correcta|Réponse correcte|Успешный ответ|Resposta correta|Odpowiedź prawidłowa|応答正常|Antwort OK|응답 정상
Response OK|Respuesta correcta|Réponse correcte|Успешный ответ|Resposta correta|Odpowiedź prawidłowa|応答正常|Antwort OK|응답 정상
Scale|Escala|Échelle|Масштаб|Escala|Skala|倍率|Faktor|배율
Offset|Desplazamiento|Décalage|Смещение|Desvio|Przesunięcie|オフセット|Offset|오프셋
Value Scaling|Escalado de valores|Mise à l’échelle des valeurs|Масштабирование значений|Escala de valores|Skalowanie wartości|数値換算|Wertskalierung|값 배율 변환
Selected Group|Grupo seleccionado|Groupe sélectionné|Выбранная группа|Grupo selecionado|Wybrana grupa|選択グループ|Ausgewählte Gruppe|선택한 그룹
Alias|Alias|Alias|Псевдоним|Nome alternativo|Alias|別名|Alias|별칭
Rows|Filas|Lignes|Строки|Linhas|Wiersze|行数|Zeilen|행 수
Hide Alias Columns|Ocultar columnas de alias|Masquer les colonnes d’alias|Скрыть столбцы псевдонимов|Ocultar colunas de nomes|Ukryj kolumny aliasów|別名列を非表示|Alias-Spalten ausblenden|별칭 열 숨기기
Address in Cell|Dirección en celda|Adresse dans la cellule|Адрес в ячейке|Endereço na célula|Adres w komórce|セル内にアドレス表示|Adresse in Zelle|셀에 주소 표시
Enron/Daniel Mode|Modo Enron/Daniel|Mode Enron/Daniel|Режим Enron/Daniel|Modo Enron/Daniel|Tryb Enron/Daniel|Enron/Daniel モード|Enron/Daniel-Modus|Enron/Daniel 모드
Request|Solicitud|Requête|Запрос|Pedido|Żądanie|要求|Anfrage|요청
Response|Respuesta|Réponse|Ответ|Resposta|Odpowiedź|応答|Antwort|응답
Read Result|Resultado de lectura|Résultat de lecture|Результат чтения|Resultado da leitura|Wynik odczytu|読み取り結果|Leseergebnis|읽기 결과
Type|Tipo|Type|Тип|Tipo|Typ|型|Typ|유형
Status|Estado|État|Состояние|Estado|Stan|状態|Status|상태
Ready|Listo|Prêt|Готово|Pronto|Gotowe|準備完了|Bereit|준비
Communication Traffic|Tráfico de comunicación|Trafic de communication|Трафик обмена|Tráfego de comunicação|Ruch komunikacyjny|通信トラフィック|Kommunikationsverkehr|통신 트래픽
Stop on Error|Detener en caso de error|Arrêter en cas d’erreur|Остановить при ошибке|Parar em caso de erro|Zatrzymaj przy błędzie|エラー時に停止|Bei Fehler stoppen|오류 시 중지
Time stamp|Marca de tiempo|Horodatage|Метка времени|Marca temporal|Znacznik czasu|タイムスタンプ|Zeitstempel|타임스탬프
Pause|Pausar|Pause|Пауза|Pausar|Wstrzymaj|一時停止|Pause|일시 중지
Resume|Continuar|Reprendre|Продолжить|Retomar|Wznów|再開|Fortsetzen|다시 시작
Export CSV|Exportar CSV|Exporter CSV|Экспорт CSV|Exportar CSV|Eksportuj CSV|CSV エクスポート|CSV exportieren|CSV 내보내기
Add Check|Añadir verificación|Ajouter une somme de contrôle|Добавить контрольную сумму|Adicionar verificação|Dodaj sumę kontrolną|チェックサム追加|Prüfsumme hinzufügen|체크섬 추가
Read Coils|Leer bobinas|Lire les bobines|Читать катушки|Ler bobinas|Odczytaj cewki|コイル読み取り|Spulen lesen|코일 읽기
Read Discrete Inputs|Leer entradas discretas|Lire les entrées discrètes|Читать дискретные входы|Ler entradas discretas|Odczytaj wejścia dyskretne|ディスクリート入力読み取り|Diskrete Eingänge lesen|개별 입력 읽기
Read Holding Registers|Leer registros de retención|Lire les registres de maintien|Читать регистры хранения|Ler registos de retenção|Odczytaj rejestry przechowujące|保持レジスタ読み取り|Halteregister lesen|유지 레지스터 읽기
Read Input Registers|Leer registros de entrada|Lire les registres d’entrée|Читать входные регистры|Ler registos de entrada|Odczytaj rejestry wejściowe|入力レジスタ読み取り|Eingangsregister lesen|입력 레지스터 읽기
Text color|Color del texto|Couleur du texte|Цвет текста|Cor do texto|Kolor tekstu|文字色|Textfarbe|글자색
Background|Fondo|Arrière-plan|Фон|Fundo|Tło|背景|Hintergrund|배경
Below color|Color inferior|Couleur inférieure|Цвет ниже порога|Cor inferior|Kolor poniżej progu|下限未満の色|Farbe unterhalb|하한 미만 색상
Above color|Color superior|Couleur supérieure|Цвет выше порога|Cor superior|Kolor powyżej progu|上限超過の色|Farbe oberhalb|상한 초과 색상
Foreground|Primer plano|Premier plan|Передний план|Primeiro plano|Pierwszy plan|前景色|Vordergrund|전경색
Below threshold (blank: off)|Umbral inferior (vacío: desactivado)|Seuil inférieur (vide : désactivé)|Нижний порог (пусто: выкл.)|Limite inferior (vazio: desligado)|Dolny próg (puste: wył.)|下限値（空欄：無効）|Unterer Grenzwert (leer: aus)|하한값 (빈칸: 끔)
Below threshold color|Color bajo el umbral|Couleur sous le seuil|Цвет ниже порога|Cor abaixo do limite|Kolor poniżej progu|下限未満の色|Farbe unter Grenzwert|하한 미만 색상
Above threshold (blank: off)|Umbral superior (vacío: desactivado)|Seuil supérieur (vide : désactivé)|Верхний порог (пусто: выкл.)|Limite superior (vazio: desligado)|Górny próg (puste: wył.)|上限値（空欄：無効）|Oberer Grenzwert (leer: aus)|상한값 (빈칸: 끔)
Above threshold color|Color sobre el umbral|Couleur au-dessus du seuil|Цвет выше порога|Cor acima do limite|Kolor powyżej progu|上限超過の色|Farbe über Grenzwert|상한 초과 색상
Address (base 0)|Dirección (base 0)|Adresse (base 0)|Адрес (с 0)|Endereço (base 0)|Adres (od 0)|アドレス（0始まり）|Adresse (ab 0)|주소 (0부터)
Value (selected Type)|Valor (tipo seleccionado)|Valeur (type sélectionné)|Значение (выбранный тип)|Valor (tipo selecionado)|Wartość (wybrany typ)|値（選択した型）|Wert (ausgewählter Typ)|값 (선택한 유형)
MIT license|Licencia MIT|Licence MIT|Лицензия MIT|Licença MIT|Licencja MIT|MIT ライセンス|MIT-Lizenz|MIT 라이선스
GitHub project|Proyecto GitHub|Projet GitHub|Проект GitHub|Projeto GitHub|Projekt GitHub|GitHub プロジェクト|GitHub-Projekt|GitHub 프로젝트
Open GitHub|Abrir GitHub|Ouvrir GitHub|Открыть GitHub|Abrir GitHub|Otwórz GitHub|GitHub を開く|GitHub öffnen|GitHub 열기
Copy link|Copiar enlace|Copier le lien|Копировать ссылку|Copiar ligação|Kopiuj link|リンクをコピー|Link kopieren|링크 복사
Context Help|Ayuda contextual|Aide contextuelle|Контекстная справка|Ajuda de contexto|Pomoc kontekstowa|コンテキストヘルプ|Kontexthilfe|상황별 도움말
Format|Formato|Format|Формат|Formato|Format|形式|Format|형식
Series|Serie|Courbe|Серия|Série|Seria|系列|Reihe|계열
Fit to Quantity|Ajustar a la cantidad|Adapter à la quantité|Подогнать по количеству|Ajustar à quantidade|Dopasuj do liczby|数量に合わせる|An Anzahl anpassen|수량에 맞춤
N/A|N/D|N/D|Н/Д|N/D|Brak|なし|k. A.|없음
Modbus Function|Función Modbus|Fonction Modbus|Функция Modbus|Função Modbus|Funkcja Modbus|Modbus 機能|Modbus-Funktion|Modbus 기능
Write|Escribir|Écrire|Записать|Escrever|Zapisz|書き込み|Schreiben|쓰기
Open list|Abrir lista|Ouvrir la liste|Открыть список|Abrir lista|Otwórz listę|リストを開く|Liste öffnen|목록 열기
Save list|Guardar lista|Enregistrer la liste|Сохранить список|Guardar lista|Zapisz listę|リストを保存|Liste speichern|목록 저장
Add to list|Añadir a la lista|Ajouter à la liste|Добавить в список|Adicionar à lista|Dodaj do listy|リストに追加|Zur Liste hinzufügen|목록에 추가
Write Address|Dirección de escritura|Adresse d’écriture|Адрес записи|Endereço de escrita|Adres zapisu|書き込みアドレス|Schreibadresse|쓰기 주소
Write Quantity|Cantidad de escritura|Quantité à écrire|Количество для записи|Quantidade de escrita|Liczba do zapisu|書き込み数量|Schreibanzahl|쓰기 수량
Read Address|Dirección de lectura|Adresse de lecture|Адрес чтения|Endereço de leitura|Adres odczytu|読み取りアドレス|Leseadresse|읽기 주소
Read Quantity|Cantidad de lectura|Quantité à lire|Количество для чтения|Quantidade de leitura|Liczba do odczytu|読み取り数量|Leseanzahl|읽기 수량
AND Mask|Máscara AND|Masque AND|Маска AND|Máscara AND|Maska AND|AND マスク|AND-Maske|AND 마스크
OR Mask|Máscara OR|Masque OR|Маска OR|Máscara OR|Maska OR|OR マスク|OR-Maske|OR 마스크
Coil|Bobina|Bobine|Катушка|Bobina|Cewka|コイル|Spule|코일
Mask bits (15 to 0)|Bits de máscara (15 a 0)|Bits du masque (15 à 0)|Биты маски (15–0)|Bits de máscara (15 a 0)|Bity maski (15–0)|マスクビット（15～0）|Maskenbits (15 bis 0)|마스크 비트 (15~0)
Mask bit|Bit de máscara|Bit du masque|Бит маски|Bit de máscara|Bit maski|マスクビット|Maskenbit|마스크 비트
Values text|Texto de valores|Texte de valeurs|Текст значений|Texto de valores|Tekst wartości|値テキスト|Wertetext|값 텍스트
Command list (*.txt)|Lista de comandos (*.txt)|Liste de commandes (*.txt)|Список команд (*.txt)|Lista de comandos (*.txt)|Lista poleceń (*.txt)|コマンドリスト (*.txt)|Befehlsliste (*.txt)|명령 목록 (*.txt)
All files|Todos los archivos|Tous les fichiers|Все файлы|Todos os ficheiros|Wszystkie pliki|すべてのファイル|Alle Dateien|모든 파일
PLC address|Dirección PLC|Adresse PLC|Адрес PLC|Endereço PLC|Adres PLC|PLC アドレス|PLC-Adresse|PLC 주소
Error|Error|Erreur|Ошибка|Erro|Błąd|エラー|Fehler|오류
Tx|Tx|Tx|Передано|Tx|Wysłano|送信|Gesendet|전송
Err|Errores|Erreurs|Ошибки|Erros|Błędy|エラー|Fehler|오류
ID|ID|ID|ID|ID|ID|ID|ID|ID
F|F|F|Код|F|F|機能|F|기능
SR|Intervalo|Intervalle|Период|Intervalo|Okres|周期|Intervall|주기
Last|Último|Dernier|Время|Último|Ostatnio|所要時間|Zuletzt|소요 시간
Enter 2..256 bytes, starting with slave ID and function.|Introduzca 2..256 bytes, empezando por el ID del esclavo y la función.|Saisissez 2 à 256 octets, en commençant par l’ID de l’esclave et la fonction.|Введите 2–256 байт, начиная с ID ведомого и кода функции.|Introduza 2..256 bytes, começando pelo ID do escravo e pela função.|Wprowadź 2–256 bajtów, zaczynając od ID urządzenia i funkcji.|スレーブ ID と機能コードから始まる 2～256 バイトを入力してください。|2–256 Bytes eingeben, beginnend mit Slave-ID und Funktion.|슬레이브 ID와 기능 코드로 시작하는 2~256바이트를 입력하세요.
Frame exceeds 256 bytes.|La trama supera los 256 bytes.|La trame dépasse 256 octets.|Кадр превышает 256 байт.|A trama excede 256 bytes.|Ramka przekracza 256 bajtów.|フレームが 256 バイトを超えています。|Der Frame überschreitet 256 Bytes.|프레임이 256바이트를 초과합니다.
Invalid command length.|Longitud de comando no válida.|Longueur de commande non valide.|Недопустимая длина команды.|Comprimento de comando inválido.|Nieprawidłowa długość polecenia.|コマンドの長さが無効です。|Ungültige Befehlslänge.|명령 길이가 올바르지 않습니다.
Command list limit is 1000.|La lista admite hasta 1000 comandos.|La liste est limitée à 1000 commandes.|Список ограничен 1000 командами.|A lista permite até 1000 comandos.|Lista może zawierać do 1000 poleceń.|コマンドリストの上限は 1000 件です。|Die Liste ist auf 1000 Befehle begrenzt.|명령 목록은 최대 1000개입니다.
Command list exceeds 1 MB.|La lista de comandos supera 1 MB.|La liste de commandes dépasse 1 Mo.|Список команд превышает 1 МБ.|A lista de comandos excede 1 MB.|Lista poleceń przekracza 1 MB.|コマンドリストが 1 MB を超えています。|Die Befehlsliste überschreitet 1 MB.|명령 목록이 1 MB를 초과합니다.
Connect before sending a command.|Conecte antes de enviar un comando.|Connectez-vous avant d’envoyer une commande.|Подключитесь перед отправкой команды.|Ligue antes de enviar um comando.|Połącz się przed wysłaniem polecenia.|接続してからコマンドを送信してください。|Vor dem Senden eines Befehls verbinden.|명령을 전송하기 전에 연결하세요.
Bus busy. Please try again.|Bus ocupado. Inténtelo de nuevo.|Bus occupé. Réessayez.|Шина занята. Повторите попытку.|Barramento ocupado. Tente novamente.|Magistrala zajęta. Spróbuj ponownie.|バスが使用中です。再試行してください。|Bus belegt. Bitte erneut versuchen.|버스가 사용 중입니다. 다시 시도하세요.
Broadcast sent (no response).|Difusión enviada (sin respuesta).|Diffusion envoyée (sans réponse).|Широковещательный запрос отправлен (без ответа).|Difusão enviada (sem resposta).|Wysłano rozgłoszenie (bez odpowiedzi).|ブロードキャスト送信済み（応答なし）。|Broadcast gesendet (ohne Antwort).|브로드캐스트 전송됨 (응답 없음).
Broadcast sent|Difusión enviada|Diffusion envoyée|Широковещательный запрос отправлен|Difusão enviada|Wysłano rozgłoszenie|ブロードキャスト送信済み|Broadcast gesendet|브로드캐스트 전송됨
Waiting for response...|Esperando respuesta...|En attente de réponse...|Ожидание ответа...|A aguardar resposta...|Oczekiwanie na odpowiedź...|応答を待機中...|Warte auf Antwort...|응답 대기 중...
Values file is too large.|El archivo de valores es demasiado grande.|Le fichier de valeurs est trop volumineux.|Файл значений слишком большой.|O ficheiro de valores é demasiado grande.|Plik wartości jest zbyt duży.|値ファイルが大きすぎます。|Die Wertedatei ist zu groß.|값 파일이 너무 큽니다.
Invalid value count, coil value or address range.|Cantidad de valores, valor de bobina o rango de direcciones no válido.|Nombre de valeurs, valeur de bobine ou plage d’adresses non valide.|Недопустимое количество значений, значение катушки или диапазон адресов.|Quantidade de valores, valor de bobina ou intervalo de endereços inválido.|Nieprawidłowa liczba wartości, wartość cewki lub zakres adresów.|値の数、コイル値、またはアドレス範囲が無効です。|Ungültige Werteanzahl, Spulenwert oder Adressbereich.|값 개수, 코일 값 또는 주소 범위가 올바르지 않습니다.
Value needs more registers; increase quantity.|El valor necesita más registros; aumente la cantidad.|La valeur nécessite plus de registres ; augmentez la quantité.|Для значения нужно больше регистров; увеличьте количество.|O valor requer mais registos; aumente a quantidade.|Wartość wymaga więcej rejestrów; zwiększ liczbę.|値に必要なレジスタが不足しています。数量を増やしてください。|Der Wert benötigt mehr Register; Anzahl erhöhen.|값에 더 많은 레지스터가 필요합니다. 수량을 늘리세요.
This function requires slave ID 1..247.|Esta función requiere un ID de esclavo entre 1 y 247.|Cette fonction nécessite un ID d’esclave de 1 à 247.|Эта функция требует ID ведомого 1–247.|Esta função requer um ID de escravo de 1 a 247.|Ta funkcja wymaga ID urządzenia od 1 do 247.|この機能には 1～247 のスレーブ ID が必要です。|Diese Funktion erfordert eine Slave-ID von 1 bis 247.|이 기능은 슬레이브 ID 1~247이 필요합니다.
Invalid register address.|Dirección de registro no válida.|Adresse de registre non valide.|Недопустимый адрес регистра.|Endereço de registo inválido.|Nieprawidłowy adres rejestru.|レジスタアドレスが無効です。|Ungültige Registeradresse.|레지스터 주소가 올바르지 않습니다.
Invalid read/write range.|Rango de lectura/escritura no válido.|Plage de lecture/écriture non valide.|Недопустимый диапазон чтения/записи.|Intervalo de leitura/escrita inválido.|Nieprawidłowy zakres odczytu/zapisu.|読み書き範囲が無効です。|Ungültiger Lese-/Schreibbereich.|읽기/쓰기 범위가 올바르지 않습니다.
Unsupported function.|Función no compatible.|Fonction non prise en charge.|Неподдерживаемая функция.|Função não suportada.|Nieobsługiwana funkcja.|サポートされていない機能です。|Nicht unterstützte Funktion.|지원하지 않는 기능입니다.
RTU requires 8 data bits. RTS timing depends on the adapter; automatic-direction RS-485 adapters are preferred.|RTU requiere 8 bits de datos. La temporización RTS depende del adaptador; se recomienda RS-485 con dirección automática.|RTU nécessite 8 bits de données. La temporisation RTS dépend de l’adaptateur ; préférez un adaptateur RS-485 à direction automatique.|RTU требует 8 бит данных. Тайминги RTS зависят от адаптера; рекомендуется RS-485 с автоматическим управлением направлением.|RTU requer 8 bits de dados. A temporização RTS depende do adaptador; recomenda-se RS-485 com direção automática.|RTU wymaga 8 bitów danych. Czas RTS zależy od adaptera; zalecany jest RS-485 z automatycznym kierunkiem.|RTU は 8 データビットが必要です。RTS のタイミングはアダプターに依存します。自動方向制御の RS-485 アダプターを推奨します。|RTU erfordert 8 Datenbits. Das RTS-Timing hängt vom Adapter ab; RS-485-Adapter mit automatischer Richtungssteuerung werden empfohlen.|RTU는 8 데이터 비트가 필요합니다. RTS 타이밍은 어댑터에 따라 다르며 자동 방향 제어 RS-485 어댑터를 권장합니다.
Save changes to the workspace?|¿Guardar los cambios del espacio de trabajo?|Enregistrer les modifications de l’espace de travail ?|Сохранить изменения рабочей области?|Guardar alterações à área de trabalho?|Zapisać zmiany obszaru roboczego?|ワークスペースの変更を保存しますか？|Änderungen am Arbeitsbereich speichern?|작업 영역의 변경 내용을 저장할까요?
Disconnect before loading a workspace.|Desconecte antes de cargar un espacio de trabajo.|Déconnectez-vous avant de charger un espace de travail.|Отключитесь перед загрузкой рабочей области.|Desligue antes de carregar uma área de trabalho.|Rozłącz się przed wczytaniem obszaru roboczego.|ワークスペースを読み込む前に切断してください。|Vor dem Laden eines Arbeitsbereichs trennen.|작업 영역을 불러오기 전에 연결을 해제하세요.
Invalid baud rate.|Velocidad en baudios no válida.|Débit en bauds non valide.|Недопустимая скорость передачи.|Velocidade em baud inválida.|Nieprawidłowa prędkość transmisji.|ボーレートが無効です。|Ungültige Baudrate.|전송 속도가 올바르지 않습니다.
Modbus RTU requires 8 data bits.|Modbus RTU requiere 8 bits de datos.|Modbus RTU nécessite 8 bits de données.|Modbus RTU требует 8 бит данных.|Modbus RTU requer 8 bits de dados.|Modbus RTU wymaga 8 bitów danych.|Modbus RTU は 8 データビットが必要です。|Modbus RTU erfordert 8 Datenbits.|Modbus RTU는 8 데이터 비트가 필요합니다.
Select a serial port.|Seleccione un puerto serie.|Sélectionnez un port série.|Выберите последовательный порт.|Selecione uma porta série.|Wybierz port szeregowy.|シリアルポートを選択してください。|Serielle Schnittstelle auswählen.|직렬 포트를 선택하세요.
Scaling is available for 16-bit and 32-bit integer groups.|El escalado está disponible para grupos de enteros de 16 y 32 bits.|La mise à l’échelle est disponible pour les groupes d’entiers de 16 et 32 bits.|Масштабирование доступно для групп 16- и 32-битных целых чисел.|A escala está disponível para grupos de inteiros de 16 e 32 bits.|Skalowanie jest dostępne dla grup liczb całkowitych 16- i 32-bitowych.|数値換算は 16 ビットおよび 32 ビット整数グループで使用できます。|Skalierung ist für 16- und 32-Bit-Ganzzahlgruppen verfügbar.|배율 변환은 16비트 및 32비트 정수 그룹에서 사용할 수 있습니다.
Displayed value = raw value × scale + offset. Writes apply the inverse transform.|Valor mostrado = valor bruto × escala + desplazamiento. La escritura aplica la transformación inversa.|Valeur affichée = valeur brute × échelle + décalage. L’écriture applique la transformation inverse.|Отображаемое значение = исходное значение × масштаб + смещение. При записи применяется обратное преобразование.|Valor apresentado = valor bruto × escala + desvio. A escrita aplica a transformação inversa.|Wartość wyświetlana = wartość surowa × skala + przesunięcie. Zapis stosuje przekształcenie odwrotne.|表示値 = 元の値 × 倍率 + オフセット。書き込み時は逆変換します。|Anzeigewert = Rohwert × Faktor + Offset. Beim Schreiben wird die Umkehrung angewendet.|표시값 = 원시값 × 배율 + 오프셋. 쓰기 시 역변환이 적용됩니다.
Enter hex numbers separated by "","" ""."" or space|Introduzca números hexadecimales separados por coma, punto o espacio|Saisissez des nombres hexadécimaux séparés par une virgule, un point ou un espace|Введите шестнадцатеричные числа через запятую, точку или пробел|Introduza números hexadecimais separados por vírgula, ponto ou espaço|Wprowadź liczby szesnastkowe oddzielone przecinkiem, kropką lub spacją|16進数をコンマ、ピリオド、または空白で区切って入力|Hexadezimalzahlen durch Komma, Punkt oder Leerzeichen trennen|16진수를 쉼표, 마침표 또는 공백으로 구분하여 입력
Close dialog on ""Response ok""|Cerrar al recibir respuesta correcta|Fermer après une réponse correcte|Закрыть при успешном ответе|Fechar após resposta correta|Zamknij po prawidłowej odpowiedzi|応答正常時に閉じる|Bei erfolgreicher Antwort schließen|정상 응답 시 닫기
Discrete inputs and input registers are read-only.|Las entradas discretas y los registros de entrada son de solo lectura.|Les entrées discrètes et les registres d’entrée sont en lecture seule.|Дискретные входы и входные регистры доступны только для чтения.|As entradas discretas e os registos de entrada são só de leitura.|Wejścia dyskretne i rejestry wejściowe są tylko do odczytu.|ディスクリート入力と入力レジスタは読み取り専用です。|Diskrete Eingänge und Eingangsregister sind schreibgeschützt.|개별 입력과 입력 레지스터는 읽기 전용입니다.
Incomplete value: increase register quantity.|Valor incompleto: aumente la cantidad de registros.|Valeur incomplète : augmentez le nombre de registres.|Неполное значение: увеличьте количество регистров.|Valor incompleto: aumente a quantidade de registos.|Niepełna wartość: zwiększ liczbę rejestrów.|値が不完全です。レジスタ数量を増やしてください。|Unvollständiger Wert: Registeranzahl erhöhen.|값이 불완전합니다. 레지스터 수량을 늘리세요.
This data area is read-only.|Esta área de datos es de solo lectura.|Cette zone de données est en lecture seule.|Эта область данных доступна только для чтения.|Esta área de dados é só de leitura.|Ten obszar danych jest tylko do odczytu.|このデータ領域は読み取り専用です。|Dieser Datenbereich ist schreibgeschützt.|이 데이터 영역은 읽기 전용입니다.
Use finite values and nonzero scale.|Use valores finitos y una escala distinta de cero.|Utilisez des valeurs finies et une échelle non nulle.|Используйте конечные значения и ненулевой масштаб.|Use valores finitos e escala diferente de zero.|Użyj wartości skończonych i niezerowej skali.|有限の値とゼロ以外の倍率を使用してください。|Endliche Werte und einen Faktor ungleich null verwenden.|유한한 값과 0이 아닌 배율을 사용하세요.
Modbus RTU desktop utility|Utilidad de escritorio Modbus RTU|Utilitaire de bureau Modbus RTU|Настольная утилита Modbus RTU|Utilitário de ambiente de trabalho Modbus RTU|Narzędzie Modbus RTU|Modbus RTU デスクトップツール|Modbus-RTU-Desktopprogramm|Modbus RTU 데스크톱 도구
Select a value cell, then Display > Link to Chart > Series 1..12|Seleccione una celda de valor y luego Visualización > Vincular al gráfico > Serie 1..12|Sélectionnez une cellule de valeur, puis Affichage > Lier au graphique > Courbe 1..12|Выберите ячейку значения, затем Отображение > Связать с графиком > Серия 1..12|Selecione uma célula de valor e depois Visualização > Associar ao gráfico > Série 1..12|Wybierz komórkę wartości, a następnie Wyświetlanie > Powiąż z wykresem > Seria 1..12|値セルを選択し、表示形式 > グラフに関連付け > 系列 1..12 を選んでください|Wertzelle wählen, dann Darstellung > Mit Diagramm verknüpfen > Reihe 1..12|값 셀을 선택한 다음 표시 형식 > 차트에 연결 > 계열 1..12 선택
Select a complete value cell.|Seleccione una celda de valor completa.|Sélectionnez une cellule de valeur complète.|Выберите полную ячейку значения.|Selecione uma célula de valor completa.|Wybierz pełną komórkę wartości.|完全な値セルを選択してください。|Vollständige Wertzelle auswählen.|완전한 값 셀을 선택하세요.
Select a serial port, baud rate, parity and stop bits. Demo uses a simulated RTU slave.|Seleccione puerto serie, baudios, paridad y bits de parada. La demo usa un esclavo RTU simulado.|Sélectionnez port série, débit, parité et bits d’arrêt. La démo utilise un esclave RTU simulé.|Выберите порт, скорость, чётность и стоповые биты. Демо использует эмулятор ведомого RTU.|Selecione porta série, velocidade, paridade e bits de paragem. A demonstração usa um escravo RTU simulado.|Wybierz port, prędkość, parzystość i bity stopu. Demo używa symulowanego urządzenia RTU.|シリアルポート、ボーレート、パリティ、ストップビットを選択します。デモは模擬 RTU スレーブを使用します。|Serielle Schnittstelle, Baudrate, Parität und Stoppbits wählen. Die Demo nutzt einen simulierten RTU-Slave.|직렬 포트, 전송 속도, 패리티, 정지 비트를 선택합니다. 데모는 모의 RTU 슬레이브를 사용합니다.
F8: slave ID, function, address, quantity and scan rate. Addresses start at zero. F5 connects; F4 disconnects; F6 reads/writes once.|F8: ID del esclavo, función, dirección, cantidad y sondeo. Direcciones desde cero. F5 conecta; F4 desconecta; F6 lee/escribe una vez.|F8 : ID de l’esclave, fonction, adresse, quantité et période. Adresses à partir de zéro. F5 connecte ; F4 déconnecte ; F6 lit/écrit une fois.|F8: ID ведомого, функция, адрес, количество и период. Адреса с нуля. F5 — подключить; F4 — отключить; F6 — однократное чтение/запись.|F8: ID do escravo, função, endereço, quantidade e intervalo. Endereços desde zero. F5 liga; F4 desliga; F6 lê/escreve uma vez.|F8: ID stacji, funkcja, adres, liczba i okres. Adresy od zera. F5 łączy; F4 rozłącza; F6 odczytuje/zapisuje raz.|F8：スレーブ ID、機能コード、アドレス、数量、スキャン間隔を設定。アドレスは 0 始まり。F5：接続、F4：切断、F6：1回読み書き。|F8: Slave-ID, Funktion, Adresse, Anzahl und Abfragezeit. Adressen ab null. F5 verbindet; F4 trennt; F6 liest/schreibt einmal.|F8: 슬레이브 ID, 기능 코드, 주소, 수량, 스캔 간격. 주소는 0부터 시작합니다. F5 연결, F4 해제, F6 한 번 읽기/쓰기.
Right-click a value to set format, byte order, font, colors or scaling.|Haga clic derecho en un valor para configurar formato, orden de bytes, fuente, colores o escalado.|Cliquez avec le bouton droit sur une valeur pour définir format, ordre des octets, police, couleurs ou échelle.|Щёлкните значение правой кнопкой для настройки формата, порядка байтов, шрифта, цветов или масштаба.|Clique num valor com o botão direito para definir formato, ordem de bytes, tipo de letra, cores ou escala.|Kliknij wartość prawym przyciskiem, aby ustawić format, kolejność bajtów, czcionkę, kolory lub skalę.|値を右クリックすると、形式、バイト順、フォント、色、数値換算を設定できます。|Rechtsklick auf einen Wert zum Einstellen von Format, Bytereihenfolge, Schriftart, Farben oder Skalierung.|값을 오른쪽 클릭하여 형식, 바이트 순서, 글꼴, 색상 또는 배율을 설정합니다.
05/06 write one value; 15/16/23 support value lists; 22 provides AND/OR mask bits.|05/06 escriben un valor; 15/16/23 admiten listas de valores; 22 ofrece máscaras AND/OR.|05/06 écrivent une valeur ; 15/16/23 acceptent des listes de valeurs ; 22 fournit les masques AND/OR.|05/06 записывают одно значение; 15/16/23 поддерживают списки значений; 22 предоставляет маски AND/OR.|05/06 escrevem um valor; 15/16/23 suportam listas de valores; 22 oferece máscaras AND/OR.|05/06 zapisują jedną wartość; 15/16/23 obsługują listy wartości; 22 udostępnia maski AND/OR.|05/06 は単一値を書き込み、15/16/23 は値リストに対応し、22 は AND/OR マスクを使用します。|05/06 schreiben einen Wert; 15/16/23 unterstützen Wertelisten; 22 bietet AND/OR-Maskenbits.|05/06은 단일 값 쓰기, 15/16/23은 값 목록, 22는 AND/OR 마스크 비트를 지원합니다.
TC sends hexadecimal commands with optional checksums. Stopping traffic capture does not stop polling.|TC envía comandos hexadecimales con verificación opcional. Detener la captura no detiene el sondeo.|TC envoie des commandes hexadécimales avec somme de contrôle facultative. Arrêter la capture ne stoppe pas la scrutation.|TC отправляет шестнадцатеричные команды с необязательной контрольной суммой. Остановка захвата не останавливает опрос.|TC envia comandos hexadecimais com verificação opcional. Parar a captura não interrompe as consultas.|TC wysyła polecenia szesnastkowe z opcjonalną sumą kontrolną. Zatrzymanie rejestracji nie zatrzymuje odpytywania.|TC はチェックサム付きの16進コマンドを送信できます。通信記録の停止はポーリングを停止しません。|TC sendet Hexadezimalbefehle mit optionaler Prüfsumme. Das Beenden der Aufzeichnung stoppt die Abfragen nicht.|TC는 선택적 체크섬과 함께 16진수 명령을 전송합니다. 통신 기록을 중지해도 폴링은 계속됩니다.
Save data as .mpdoc or workspaces as .mprtu. Compatible .mbp files can be imported.|Guarde datos como .mpdoc y espacios de trabajo como .mprtu. Se pueden importar archivos .mbp compatibles.|Enregistrez les données en .mpdoc et les espaces de travail en .mprtu. Les fichiers .mbp compatibles sont importables.|Сохраняйте данные в .mpdoc, рабочие области — в .mprtu. Можно импортировать совместимые .mbp.|Guarde dados como .mpdoc e áreas de trabalho como .mprtu. Pode importar ficheiros .mbp compatíveis.|Zapisuj dane jako .mpdoc, a obszary robocze jako .mprtu. Można importować zgodne pliki .mbp.|データは .mpdoc、ワークスペースは .mprtu で保存します。互換性のある .mbp ファイルをインポートできます。|Daten als .mpdoc und Arbeitsbereiche als .mprtu speichern. Kompatible .mbp-Dateien können importiert werden.|데이터는 .mpdoc, 작업 영역은 .mprtu로 저장합니다. 호환되는 .mbp 파일을 가져올 수 있습니다.
Choose a language under Language. English is the default; your selection is saved.|Elija el idioma en Language. Inglés es el predeterminado; se guarda su elección.|Choisissez la langue dans Language. L’anglais est la langue par défaut ; votre choix est enregistré.|Выберите язык в меню Language. По умолчанию — английский; выбор сохраняется.|Escolha o idioma em Language. O inglês é a predefinição; a escolha é guardada.|Wybierz język w Language. Domyślny jest angielski; wybór jest zapisywany.|Language で言語を選択します。既定は英語で、選択は保存されます。|Sprache unter Language wählen. Standard ist Englisch; die Auswahl wird gespeichert.|Language에서 언어를 선택합니다. 기본 언어는 영어이며 선택 내용은 저장됩니다.
Only one application can use a serial port at a time.|Solo una aplicación puede usar un puerto serie a la vez.|Une seule application peut utiliser un port série à la fois.|Порт может использоваться только одним приложением одновременно.|Só uma aplicação pode usar uma porta série de cada vez.|Portu szeregowego może używać tylko jedna aplikacja naraz.|同じシリアルポートは同時に1つのアプリケーションのみ使用できます。|Eine serielle Schnittstelle kann nur von einer Anwendung gleichzeitig verwendet werden.|직렬 포트는 한 번에 하나의 프로그램만 사용할 수 있습니다.
";
    }
}
