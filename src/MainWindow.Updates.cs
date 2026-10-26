using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ModbusGetFree
{
    public sealed class UpdatePrompt : EditorDialog
    {
        public UpdatePrompt(string version) : base("Software update", 260)
        {
            ClientSize = new Size(760, 230);
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 4 };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            panel.Controls.Add(new Label { Text = "A newer version is available", AutoSize = true }, 0, 0);
            panel.Controls.Add(new Label { Name = "UpdateVersions", Text = "v" + AppBrand.Version + "  →  " + version, AutoSize = true }, 0, 1);
            panel.Controls.Add(new Label { Text = "Background download: apply on next launch. This choice is for this session only.", Dock = DockStyle.Fill }, 0, 2);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            foreach (var item in new[] { Tuple.Create("Upgrade now", DialogResult.Yes), Tuple.Create("Upgrade in background", DialogResult.No), Tuple.Create("Skip this time", DialogResult.Cancel) })
                buttons.Controls.Add(new Button { Name = item.Item1, Text = item.Item1, Width = 230, Height = 34, DialogResult = item.Item2 });
            panel.Controls.Add(buttons, 0, 3);
            CancelButton = (Button)buttons.Controls[2];
            Controls.Clear(); Controls.Add(panel);
        }
    }
    public partial class MainWindow
    {
        bool allowUpdates;
        CancellationTokenSource updateDownload;
        string installUpdate;
        async Task StartUpdates()
        {
            if (!allowUpdates) return;
            updateDownload = new CancellationTokenSource();
            FormClosed += (s, e) => updateDownload.Cancel();
            try
            {
                string pending = null;
                try { pending = UpdateService.FindPending(Application.ExecutablePath); }
                catch (Exception ex) { AddTraffic(Language.Text("Update check or download failed; the current version is unchanged.") + " " + ex.Message); }
                if (pending != null)
                {
                    installUpdate = pending;
                    Close();
                    return;
                }
                var release = await UpdateService.CheckAsync(AppBrand.RepositoryUrl, updateDownload.Token);
                if (release == null || IsDisposed || closing || !allowUpdates) return;
                await OfferUpdate(release);
            }
            catch (Exception ex)
            {
                if (!IsDisposed && !closing && !(ex is OperationCanceledException))
                    AddTraffic(Language.Text("Update check or download failed; the current version is unchanged.") + " " + ex.Message);
            }
        }
        internal async Task OfferUpdate(UpdateRelease release)
        {
                DialogResult choice;
                using (var prompt = new UpdatePrompt(release.Version)) choice = prompt.ShowDialog(this);
                if (choice == DialogResult.Cancel || IsDisposed || closing || !allowUpdates) return;
                bool immediate = choice == DialogResult.Yes;
                // A modeless progress window keeps polling and editing responsive during the download.
                EditorDialog progress = null;
                if (immediate)
                {
                    progress = new EditorDialog("Downloading update", 160);
                    progress.ClientSize = new Size(580, 130);
                    progress.Controls.Clear();
                    progress.Controls.Add(new Label { Text = "Downloading update. Close this window to cancel.", Dock = DockStyle.Fill, Padding = new Padding(16) });
                    progress.FormClosed += (s, e) => updateDownload.Cancel();
                    progress.Show(this);
                }
                try
                {
                    string manifest = await UpdateService.DownloadAsync(release, Application.ExecutablePath, updateDownload.Token);
                    if (IsDisposed || closing || !allowUpdates) return;
                    if (immediate)
                    {
                        if (progress != null) { progress.Hide(); progress.Dispose(); progress = null; }
                        installUpdate = manifest;
                        Close();
                        if (!closing) installUpdate = null; // Unsaved-work confirmation was cancelled.
                    }
                    else AddTraffic(Language.Text("Update downloaded. It will be applied on next launch."));
                }
                finally { if (progress != null) { progress.Hide(); progress.Dispose(); } }
        }
        bool LaunchUpdateBeforeClosing()
        {
            if (installUpdate == null) return true;
            try
            {
                using (Process helper = UpdateService.LaunchHelper(installUpdate, Application.ExecutablePath))
                    if (helper == null) throw new InvalidOperationException("Update helper could not be started.");
                return true;
            }
            catch (Exception ex)
            {
                installUpdate = null;
                LocalMessageBox.Show(this, Language.Text("Update installation failed. Your previous version was kept.") + "\r\n" + ex.Message);
                return false;
            }
        }
    }
}

