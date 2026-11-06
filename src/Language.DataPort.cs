namespace ModbusGetFree
{
    public static partial class Language
    {
        static void InitializeDataPort()
        {
            foreach (string line in DataPortCatalog.Split('\n'))
            {
                string[] cells = line.TrimEnd('\r').Split('|');
                if (cells.Length == 1) continue;
                if (cells.Length != 10) throw new System.InvalidOperationException("Invalid Data Port translation: " + cells[0]);
                zh[cells[0]] = cells[1]; en[cells[1]] = cells[0];
                var row = new string[9]; row[0] = cells[0];
                System.Array.Copy(cells, 2, row, 1, 8);
                additional[Normalize(cells[0])] = row;
            }
        }
        const string DataPortCatalog = @"Data Port|数据接口|Puerto de datos|Port de données|Порт данных|Porta de dados|Port danych|データポート|Datenschnittstelle|데이터 포트
Enable Data Port|启用数据接口|Activar puerto de datos|Activer le port de données|Включить порт данных|Ativar porta de dados|Włącz port danych|データポートを有効化|Datenschnittstelle aktivieren|데이터 포트 활성화
Stop Data Port|停止数据接口|Detener puerto de datos|Arrêter le port de données|Остановить порт данных|Parar porta de dados|Zatrzymaj port danych|データポートを停止|Datenschnittstelle stoppen|데이터 포트 중지
Open demo page|打开演示页|Abrir demostración|Ouvrir la démonstration|Открыть демонстрацию|Abrir demonstração|Otwórz demonstrację|デモページを開く|Demoseite öffnen|데모 페이지 열기
Copy connection info|复制连接信息|Copiar datos de conexión|Copier les infos de connexion|Копировать данные подключения|Copiar dados de conexão|Kopiuj dane połączenia|接続情報をコピー|Verbindungsdaten kopieren|연결 정보 복사
Data Port is off.|数据接口已关闭。|El puerto de datos está desactivado.|Le port de données est désactivé.|Порт данных выключен.|A porta de dados está desativada.|Port danych jest wyłączony.|データポートは無効です。|Die Datenschnittstelle ist deaktiviert.|데이터 포트가 비활성화되어 있습니다.
Only local read access. No device writes or extra polling.|仅允许本机读取，不写入设备，也不增加设备轮询。|Solo lectura local. Sin escrituras en el dispositivo ni sondeos adicionales.|Lecture locale uniquement. Aucune écriture sur l’appareil ni interrogation supplémentaire.|Только локальное чтение. Без записи в устройство и дополнительного опроса.|Apenas leitura local. Sem gravação no dispositivo nem consultas adicionais.|Tylko odczyt lokalny. Bez zapisu do urządzenia i dodatkowego odpytywania.|ローカルでの読み取り専用です。機器への書き込みや追加のポーリングは行いません。|Nur lokaler Lesezugriff. Keine Gerätezugriffe zum Schreiben oder zusätzliche Abfragen.|로컬 읽기 전용입니다. 장치에 쓰거나 추가 폴링을 수행하지 않습니다.
Data Port is on. Closing the app or stopping the port revokes access.|数据接口已启用。关闭软件或停止接口后，访问权限失效。|El puerto de datos está activo. Cerrar la aplicación o detener el puerto revoca el acceso.|Le port de données est actif. Fermer l’application ou arrêter le port révoque l’accès.|Порт данных включён. Закрытие программы или остановка порта прекращает доступ.|A porta de dados está ativa. Fechar o aplicativo ou parar a porta revoga o acesso.|Port danych jest włączony. Zamknięcie programu lub zatrzymanie portu kończy dostęp.|データポートは有効です。アプリを閉じるかポートを停止するとアクセスは無効になります。|Die Datenschnittstelle ist aktiv. Beim Schließen der Anwendung oder Stoppen der Schnittstelle erlischt der Zugriff.|데이터 포트가 활성화되어 있습니다. 앱을 닫거나 포트를 중지하면 접근 권한이 해제됩니다.
Access token|访问令牌|Token de acceso|Jeton d’accès|Токен доступа|Token de acesso|Token dostępu|アクセストークン|Zugriffstoken|접근 토큰
MCP setup: see {0}.|MCP 配置：参见 {0}。|Configuración MCP: consulte {0}.|Configuration MCP : voir {0}.|Настройка MCP: см. {0}.|Configuração MCP: consulte {0}.|Konfiguracja MCP: zobacz {0}.|MCP の設定：{0} を参照してください。|MCP-Einrichtung: siehe {0}.|MCP 설정: {0}을(를) 참조하세요.
Current snapshots only; no history. Units require register documentation.|仅提供当前快照，不含历史记录。单位须以寄存器说明为准。|Solo instantáneas actuales, sin historial. Las unidades requieren la documentación de registros.|Instantanés actuels uniquement, sans historique. Les unités nécessitent la documentation des registres.|Только текущие снимки, без истории. Единицы уточняйте в описании регистров.|Apenas instantâneos atuais, sem histórico. As unidades exigem a documentação dos registradores.|Tylko bieżące migawki, bez historii. Jednostki wymagają dokumentacji rejestrów.|現在のスナップショットのみで、履歴はありません。単位はレジスタ仕様書で確認してください。|Nur aktuelle Momentaufnahmen, kein Verlauf. Einheiten sind der Registerdokumentation zu entnehmen.|현재 스냅샷만 제공하며 이력은 없습니다. 단위는 레지스터 문서에서 확인해야 합니다.
Simulated data is marked demo. Share the token only with trusted tools.|模拟数据标记为 demo。请仅向可信工具提供令牌。|Los datos simulados se marcan como demo. Comparta el token solo con herramientas de confianza.|Les données simulées portent la mention demo. Ne partagez le jeton qu’avec des outils de confiance.|Моделируемые данные отмечены как demo. Передавайте токен только доверенным инструментам.|Os dados simulados são marcados como demo. Compartilhe o token apenas com ferramentas confiáveis.|Dane symulowane są oznaczone jako demo. Udostępniaj token tylko zaufanym narzędziom.|シミュレーションデータには demo と表示されます。トークンは信頼できるツールにのみ渡してください。|Simulierte Daten sind mit demo gekennzeichnet. Geben Sie den Token nur an vertrauenswürdige Tools weiter.|시뮬레이션 데이터는 demo로 표시됩니다. 신뢰할 수 있는 도구에만 토큰을 제공하세요.
Unable to start Data Port|无法启动数据接口|No se pudo iniciar el puerto de datos|Impossible de démarrer le port de données|Не удалось запустить порт данных|Não foi possível iniciar a porta de dados|Nie można uruchomić portu danych|データポートを起動できません|Datenschnittstelle konnte nicht gestartet werden|데이터 포트를 시작할 수 없습니다
Demo mode - simulated data, not measurements|演示模式：模拟数据，非实测|Demostración: datos simulados, no mediciones|Démonstration : données simulées, pas des mesures|Демонстрация: моделируемые данные, не измерения|Demonstração: dados simulados, não medições|Demonstracja: dane symulowane, nie pomiary|デモモード：シミュレーションデータ、実測値ではありません|Demomodus: simulierte Daten, keine Messwerte|데모 모드: 시뮬레이션 데이터이며 실측값이 아닙니다
Simulated temperature|模拟温度|Temperatura simulada|Température simulée|Моделируемая температура|Temperatura simulada|Symulowana temperatura|シミュレーション温度|Simulierte Temperatur|시뮬레이션 온도
Simulated pH|模拟 pH|pH simulado|pH simulé|Моделируемый pH|pH simulado|Symulowane pH|シミュレーション pH|Simulierter pH-Wert|시뮬레이션 pH
Temperature A (°C)|温度 A（℃）|Temperatura A (°C)|Température A (°C)|Температура A (°C)|Temperatura A (°C)|Temperatura A (°C)|温度 A (°C)|Temperatur A (°C)|온도 A (°C)
Temperature B (°C)|温度 B（℃）|Temperatura B (°C)|Température B (°C)|Температура B (°C)|Temperatura B (°C)|Temperatura B (°C)|温度 B (°C)|Temperatur B (°C)|온도 B (°C)
Temperature C (°C)|温度 C（℃）|Temperatura C (°C)|Température C (°C)|Температура C (°C)|Temperatura C (°C)|Temperatura C (°C)|温度 C (°C)|Temperatur C (°C)|온도 C (°C)
pH channel A|pH 通道 A|Canal pH A|Canal pH A|Канал pH A|Canal de pH A|Kanał pH A|pH チャンネル A|pH-Kanal A|pH 채널 A
pH channel B|pH 通道 B|Canal pH B|Canal pH B|Канал pH B|Canal de pH B|Kanał pH B|pH チャンネル B|pH-Kanal B|pH 채널 B";
    }
}
