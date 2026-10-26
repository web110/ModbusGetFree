namespace ModbusGetFree
{
    public static partial class Language
    {
        static void InitializeUpdates()
        {
            foreach (string line in UpdateCatalog.Split('\n'))
            {
                string[] cells = line.TrimEnd('\r').Split('|');
                if (cells.Length == 1) continue;
                if (cells.Length != 10) throw new System.InvalidOperationException("Invalid update translation.");
                zh[cells[0]] = cells[1]; en[cells[1]] = cells[0];
                var row = new string[9]; row[0] = cells[0];
                System.Array.Copy(cells, 2, row, 1, 8);
                additional[Normalize(cells[0])] = row;
            }
        }
        const string UpdateCatalog = @"Allow updates: check GitHub at startup|允许自动升级：启动时检查 GitHub|Permitir actualizaciones: comprobar GitHub al iniciar|Autoriser les mises à jour : vérifier GitHub au démarrage|Разрешить обновления: проверять GitHub при запуске|Permitir atualizações: verificar o GitHub ao iniciar|Zezwalaj na aktualizacje: sprawdzaj GitHub przy starcie|更新を許可：起動時に GitHub を確認|Updates erlauben: GitHub beim Start prüfen|업데이트 허용: 시작 시 GitHub 확인
Software update|软件升级|Actualización del software|Mise à jour du logiciel|Обновление программы|Atualização do software|Aktualizacja programu|ソフトウェアの更新|Softwareupdate|소프트웨어 업데이트
A newer version is available|发现新版本|Hay una nueva versión disponible|Une nouvelle version est disponible|Доступна новая версия|Está disponível uma nova versão|Dostępna jest nowa wersja|新しいバージョンがあります|Eine neue Version ist verfügbar|새 버전을 사용할 수 있습니다
Background download: apply on next launch. This choice is for this session only.|后台下载，下次启动应用更新；此选择仅本次有效。|Descarga en segundo plano; se aplica al reiniciar. Elección solo para esta sesión.|Téléchargement en arrière-plan ; installation au prochain lancement. Choix limité à cette session.|Загрузка в фоне; установка при следующем запуске. Выбор только для этого сеанса.|Transferência em segundo plano; aplicar no próximo arranque. Escolha só para esta sessão.|Pobierz w tle; zastosuj przy następnym uruchomieniu. Wybór tylko na tę sesję.|バックグラウンドでダウンロードし、次回起動時に適用します。今回のみの選択です。|Download im Hintergrund; Anwendung beim nächsten Start. Auswahl nur für diese Sitzung.|백그라운드 다운로드 후 다음 실행 시 적용합니다. 이번에만 유효한 선택입니다.
Upgrade now|立即升级|Actualizar ahora|Mettre à jour maintenant|Обновить сейчас|Atualizar agora|Aktualizuj teraz|今すぐ更新|Jetzt aktualisieren|지금 업데이트
Upgrade in background|后台升级|Actualizar en segundo plano|Mise à jour en arrière-plan|Обновить в фоне|Atualizar em segundo plano|Aktualizuj w tle|バックグラウンド更新|Im Hintergrund aktualisieren|백그라운드 업데이트
Skip this time|本次不升级|Omitir esta vez|Ignorer cette fois|Пропустить сейчас|Ignorar desta vez|Pomiń tym razem|今回は更新しない|Diesmal überspringen|이번에는 건너뛰기
Downloading update|正在下载更新|Descargando actualización|Téléchargement de la mise à jour|Загрузка обновления|A transferir atualização|Pobieranie aktualizacji|更新をダウンロード中|Update wird heruntergeladen|업데이트 다운로드 중
Downloading update. Close this window to cancel.|正在下载更新，关闭此窗口可取消。|Descargando actualización. Cierre esta ventana para cancelar.|Téléchargement en cours. Fermez cette fenêtre pour annuler.|Идёт загрузка. Закройте окно для отмены.|A transferir atualização. Feche esta janela para cancelar.|Trwa pobieranie. Zamknij to okno, aby anulować.|ダウンロード中です。このウィンドウを閉じると中止します。|Update wird heruntergeladen. Zum Abbrechen dieses Fenster schließen.|업데이트 다운로드 중입니다. 취소하려면 이 창을 닫으세요.
Update downloaded. It will be applied on next launch.|更新已下载，将在下次启动时应用。|Actualización descargada. Se aplicará al iniciar de nuevo.|Mise à jour téléchargée. Elle sera appliquée au prochain lancement.|Обновление загружено. Оно будет установлено при следующем запуске.|Atualização transferida. Será aplicada no próximo arranque.|Aktualizacja pobrana. Zostanie zastosowana przy następnym uruchomieniu.|ダウンロード完了。次回起動時に更新を適用します。|Update heruntergeladen. Es wird beim nächsten Start angewendet.|업데이트 다운로드 완료. 다음 실행 시 적용됩니다.
Update check or download failed; the current version is unchanged.|检查或下载更新失败，当前版本保持不变。|Error al comprobar o descargar. Se conserva la versión actual.|Échec de la vérification ou du téléchargement. La version actuelle est conservée.|Не удалось проверить или загрузить обновление. Текущая версия сохранена.|Falha ao verificar ou transferir. A versão atual é mantida.|Nie udało się sprawdzić lub pobrać aktualizacji. Bieżąca wersja pozostaje bez zmian.|更新の確認またはダウンロードに失敗しました。現在のバージョンを維持します。|Updateprüfung oder Download fehlgeschlagen. Die aktuelle Version bleibt erhalten.|업데이트 확인 또는 다운로드 실패. 현재 버전은 유지됩니다.
Update installation failed. Your previous version was kept.|更新安装失败，已保留原版本。|Error de instalación. Se ha conservado la versión anterior.|Échec de l’installation. La version précédente a été conservée.|Установка не удалась. Предыдущая версия сохранена.|Falha na instalação. A versão anterior foi mantida.|Instalacja nie powiodła się. Zachowano poprzednią wersję.|更新のインストールに失敗しました。以前のバージョンを保持しました。|Updateinstallation fehlgeschlagen. Die vorherige Version wurde beibehalten.|업데이트 설치 실패. 이전 버전을 유지했습니다.";
    }
}
