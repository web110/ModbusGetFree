# 自动升级 / Automatic updates

## 用户操作

在 **Help → About ModbusGetFree** 中勾选“允许自动升级：启动时检查 GitHub”。默认关闭，选择会保存到 `preferences.xml`。开关打开后，下次启动检查 `github-repository.txt` 指向仓库的最新正式 Release。

发现比当前版本更新、且附有兼容 ZIP 安装包的版本时，显示三个选项：

- **立即升级**：下载并校验，走现有的未保存内容确认流程，关闭软件后安装并重新启动。取消关闭时不会强制退出，已下载包留待下次启动。
- **后台升级**：在后台下载并校验，当前软件继续使用；下次启动时应用下载好的更新。“后台升级”选择只对本次有效，不会保存为永久策略。
- **本次不升级**：本次不下载；以后启动时仍可再次提醒。关闭提醒窗口等同此选项。

关闭允许升级开关后，不进行启动检查，也不应用待安装包；正在进行的下载会取消。没有 Release、断网或检查失败不会阻止串口操作；检查失败写入通信记录。后台下载未完成即退出时不会安装不完整包，下次启动重新检查。

更新仅覆盖程序、图片/图标资源、README 和 LICENSE。串口设置、语言选择、用户 `.mpdoc` / `.mprtu` / `.mbp` 文件不会被升级包覆盖。程序目录必须可写；本功能不会请求管理员权限。替换失败时回滚已替换文件，并在更新缓存中保留错误记录。

每次启动只检查一次 GitHub。连接失败或超时后，本次运行不重连、不重试，等下次启动再尝试，不影响串口通信。

## GitHub 发布要求

1. 同步修改 `src/AppBrand.cs` 的 `Version` 和 `src/UpdateService.cs` 中两个程序集版本属性，例如 `1.11` / `1.11.0.0`。
2. 运行 `./make-update-package.ps1`，生成 `release/ModbusGetFree-update.zip`。这是更新用压缩包，不包含用户设置和文档数据。
3. 在 `github-repository.txt` 对应仓库发布正式 Release，标签为相同数字版本，例如 `v1.11`。草稿和预发行版不会触发更新。
4. 将 `ModbusGetFree-update.zip` 上传为 Release 附件。只推送源码或修改 README 不会触发升级。更新包内 `ModbusGetFree.exe` 的程序集版本必须与标签一致。
5. 优先选择名称精确为 `ModbusGetFree-update.zip` 的附件。没有此附件时，只允许唯一一个名称以 `ModbusGetFree` 开头、以 `.zip` 结尾的兼容包；多个候选包不会被随意选择。

使用公开 GitHub Release API，不在程序中嵌入访问令牌。下载通过 HTTPS，校验附件大小、GitHub 提供的 SHA-256 digest（如有）、解压路径、程序集名称与版本；安装前再次检查暂存文件哈希。更新 ZIP 最大 150 MiB，解压后总大小最大 150 MiB。暂存位置为本地应用数据目录 `ModbusGetFree/Updates`，按程序路径隔离。

构建及运行要求 Windows .NET Framework 4.5 或更高版本。源代码内包含更新所需程序集引用。

## Verification

Run `./test-updates.ps1` for isolated update tests and `./test-ui-regression.ps1` for the existing UI, language and polling tests. Update tests use a local fixture and their own cache. They do not publish a release or change the original D: project.

The About checkbox is off by default and persists its value. When enabled, startup checks the latest published stable GitHub Release with a compatible ZIP asset. **Upgrade now** downloads, validates, follows the unsaved-work confirmation, installs after the application closes and restarts it. **Upgrade in background** downloads only and applies on the next launch; the choice is session-only. **Skip this time** does not download and does not permanently suppress future reminders.

Turning the checkbox off disables both startup checks and applying staged updates. Runtime assets and documentation may be replaced; preferences and user documents are preserved. Network/check failures do not block serial use. A writable installation directory is required; no elevation is requested. For publishing, keep the display and assembly versions aligned, run `make-update-package.ps1`, publish a stable tag such as `v1.11`, and attach `ModbusGetFree-update.zip` to that release.

GitHub is checked only once per launch. If the connection fails or times out, there is no reconnection or retry during the current session; the next attempt is on the next launch. Serial communication is unaffected.
