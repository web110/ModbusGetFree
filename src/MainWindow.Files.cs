using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace ModbusGetFree
{
    /// <summary>
    /// 工作区、单窗口文件和偏好配置的保存、载入及参数校验。
    /// </summary>
    public partial class MainWindow : Form
    {
        bool ConfirmDiscard()
        {
            if (!dirty)
            {
                return true;
            }

            var answer = LocalMessageBox.Show(this, "Save changes to the workspace?",
                 "Workspace", MessageBoxButtons.YesNoCancel);
            if (answer == DialogResult.Cancel)
            {
                return false;
            }

            if (answer == DialogResult.Yes)
            {
                return SaveWorkspace(false);
            }

            return true;
        }

        public bool SaveWorkspace(bool saveAs)
        {
            string target = workspacePath;
            if (saveAs || target == null)
            {
                using (var d = new SaveFileDialog
                {
                    Filter = "RTU Workspace|*.mprtu",
                    FileName = "workspace.mprtu"
                }

                )
                {
                    if (d.ShowDialog(this) != DialogResult.OK)
                    {
                        return false;
                    }

                    target = d.FileName;
                }
            }

            try
            {
                var workspace = new Workspace
                {
                    Connection = Settings,
                    Documents = MdiChildren.OfType<DataWindow>().Reverse().Select(d => d.Snapshot()).ToList()
                };
                var serializer = new XmlSerializer(typeof(Workspace));
                using (var stream = File.Create(target))
                {
                    serializer.Serialize(stream, workspace);
                }

                workspacePath = target;
                startupPlaceholder = null;
                dirty = false;
                Text = "ModbusGetFree — " + Path.GetFileName(target);
                return true;
            }
            catch (Exception ex)
            {
                LocalMessageBox.Show(this, ex.Message, "Save Error");
                return false;
            }
        }

        public void OpenWorkspace()
        {
            if (InFlight)
            {
                return;
            }

            if (!ConfirmDiscard())
            {
                return;
            }

            using (var d = new OpenFileDialog
            {
                Filter = "RTU Workspace|*.mprtu"
            }

            )
            {
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        Workspace w;
                        using (var reader = System.Xml.XmlReader.Create(d.FileName,
                             new System.Xml.XmlReaderSettings
                             {
                                 DtdProcessing = System.Xml.DtdProcessing.Prohibit,
                                 XmlResolver = null
                             }))
                        {
                            w = (Workspace)new XmlSerializer(typeof(Workspace)).Deserialize(reader);
                        }

                        ValidateWorkspace(w);
                        if (Bus.Connected)
                        {
                            LocalMessageBox.Show(this, "Disconnect before loading a workspace.");
                            return;
                        }

                        foreach (var child in MdiChildren)
                        {
                            child.Close();
                        }

                        Settings = w.Connection;
                        foreach (var state in w.Documents)
                        {
                            NewDocument(state);
                        }

                        workspacePath = d.FileName;
                        dirty = false;
                        Text = "ModbusGetFree — " + Path.GetFileName(workspacePath);
                    }
                    catch (Exception ex)
                    {
                        LocalMessageBox.Show(this, ex.Message, "Open Error");
                    }
                }
            }
        }

        public static void ValidateWorkspace(Workspace w)
        {
            if (w == null || w.Connection == null || w.Documents == null || w.Documents.Count > 100)
            {
                throw new IOException("Invalid workspace.");
            }

            if (w.Connection.Baud < 50 || w.Connection.Timeout < 100 || w.Connection.Delay < 2
                || w.Connection.DataBits != 8)
            {
                throw new IOException("Invalid serial settings.");
            }

            foreach (var d in w.Documents)
            {
                bool enron = d.Enron && (d.Function == 3 || d.Function == 4
                    || d.Function == 16) && ((d.Address >= 5000 && d.Address < 6000) || (d.Address >= 7000
                    && d.Address < 8000));
                if (enron)
                {
                    Modbus.EnronRequest(d.Unit, d.Function, d.Address, d.Quantity,
                         d.Function > 4 ? new ushort[d.Quantity * 2] : null);
                }
                else
                {
                    Modbus.Request(d.Unit, d.Function, d.Address, d.Quantity,
                         d.Function > 4 ? new ushort[d.Quantity] : null);
                }

                if ((enron && (d.Format < 4 || d.Format > 6)) || d.Format < 0
                    || d.Format >= ValueCodec.Formats.Length || d.Order < 0 || d.Order > 3 || (d.Rows != 0
                    && d.Rows != 10 && d.Rows != 20 && d.Rows != 50 && d.Rows != 100) || d.ScanRate < 0
                    || d.ScanRate > 3600000 || d.Width < 200 || d.Height < 150 || d.Width > 10000
                    || d.Height > 10000 || d.Scale == 0 || double.IsNaN(d.Scale) || double.IsInfinity(d.Scale)
                    || double.IsNaN(d.Offset) || double.IsInfinity(d.Offset))
                {
                    throw new IOException("Invalid document settings.");
                }

                if (d.Definitions == null)
                {
                    d.Definitions = new List<RegisterDefinition>();
                }

                if (d.Definitions.Count > 65536)
                {
                    throw new IOException("Too many register definitions.");
                }

                int previousEnd = -1;
                foreach (var definition in d.Definitions.OrderBy(item => item == null ? -1 : item.Address))
                {
                    if (definition == null || definition.Address < 0 || definition.Address > 65535 ||
                        definition.Format < 0 || definition.Format >= ValueCodec.Formats.Length ||
                        definition.Order < 0 || definition.Order > 3 || definition.Scale == 0 ||
                        double.IsNaN(definition.Scale) || double.IsInfinity(definition.Scale) ||
                        double.IsNaN(definition.Offset) || double.IsInfinity(definition.Offset) ||
                        double.IsNaN(definition.ColorBelow) || double.IsInfinity(definition.ColorBelow) ||
                        double.IsNaN(definition.ColorAbove) || double.IsInfinity(definition.ColorAbove) ||
                        definition.ColorBelow > definition.ColorAbove ||
                        string.IsNullOrWhiteSpace(definition.FontName) || definition.FontSize < 1 ||
                        definition.FontSize > 200 || float.IsNaN(definition.FontSize) ||
                        float.IsInfinity(definition.FontSize) || (definition.FontStyle & ~15) != 0)
                    {
                        throw new IOException("Invalid register definition.");
                    }

                    int width = ValueCodec.Width(definition.Format);
                    if (enron && width != 2)
                    {
                        throw new IOException("Enron definitions require a 32-bit format.");
                    }

                    int end = definition.Address + width / (enron ? 2 : 1);
                    if (definition.Address < previousEnd || end > 65536)
                    {
                        throw new IOException("Overlapping or out-of-range register definitions.");
                    }

                    previousEnd = end;
                }

                if (d.Values == null)
                {
                    d.Values = new ushort[0];
                }

                if (d.Names == null)
                {
                    d.Names = new string[0];
                }
            }
        }

        void SavePreferences()
        {
            try
            {
                var prefs = new Preferences
                {
                    Connection = Settings,
                    AutoConnect = autoConnect,
                    Language = ModbusGetFree.Language.Code,
                    Default = defaultDocument,
                    Recent = recentFiles
                };
                using (var stream = File.Create(PrefsPath))
                {
                    new XmlSerializer(typeof(Preferences)).Serialize(stream, prefs);
                }
            }
            catch (Exception ex)
            {
                LocalMessageBox.Show(this, "Preferences could not be saved: " + ex.Message);
            }
        }

        void LoadPreferences()
        {
            try
            {
                if (!File.Exists(PrefsPath))
                {
                    return;
                }

                using (var reader = System.Xml.XmlReader.Create(PrefsPath, new System.Xml.XmlReaderSettings
                {
                    DtdProcessing = System.Xml.DtdProcessing.Prohibit,
                    XmlResolver = null
                }))
                {
                    var prefs = (Preferences)new XmlSerializer(typeof(Preferences)).Deserialize(reader);
                    ValidateWorkspace(new Workspace
                    {
                        Connection = prefs.Connection,
                        Documents = prefs.Default == null ? new List<DocumentState>()
                            : new List<DocumentState> { prefs.Default }
                    });
                    Language.Code = prefs.Language;
                    Language.Refresh();
                    Settings = prefs.Connection;
                    autoConnect = prefs.AutoConnect;
                    defaultDocument = prefs.Default;
                    recentFiles = prefs.Recent ?? new List<string>();
                }
            }
            catch (Exception ex)
            {
                AddTraffic("Preferences ignored: " + ex.Message);
            }
        }

        public static DocumentState CloneDocument(DocumentState state)
        {
            var serializer = new XmlSerializer(typeof(DocumentState));
            using (var stream = new MemoryStream())
            {
                serializer.Serialize(stream, state);
                stream.Position = 0;
                return (DocumentState)serializer.Deserialize(stream);
            }
        }

        public void RememberFile(string file)
        {
            recentFiles.Remove(file);
            recentFiles.Insert(0, file);
            if (recentFiles.Count > 10)
            {
                recentFiles.RemoveAt(10);
            }

            SavePreferences();
        }

        void ChooseOpenDocument()
        {
            using (var dialog = new OpenFileDialog
            {
                Filter = "Modbus documents|*.mpdoc;*.mbp|ModbusGetFree project|*.mbp|RTU Data Window|*.mpdoc|All files|*.*"
            }

            )
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    OpenDocument(dialog.FileName);
                }
            }
        }

        public void OpenDocument(string file)
        {
            try
            {
                DocumentState state;
                bool imported = string.Equals(Path.GetExtension(file), ".mbp", StringComparison.OrdinalIgnoreCase);
                if (imported)
                {
                    state = MbpImporter.Load(file);
                }
                else
                using (var reader = System.Xml.XmlReader.Create(file, new System.Xml.XmlReaderSettings
                {
                    DtdProcessing = System.Xml.DtdProcessing.Prohibit,
                    XmlResolver = null
                }))
                {
                    state = (DocumentState)new XmlSerializer(typeof(DocumentState)).Deserialize(reader);
                }

                ValidateWorkspace(new Workspace { Connection = Settings, Documents = new List<DocumentState> { state } });
                // 仅替换本次启动自动生成且未编辑的占位窗口。显式新建和已编辑窗口继续保留。
                var placeholder = startupPlaceholder;
                NewDocument(state).DocumentPath = imported ? null : file;
                if (placeholder != null && !placeholder.IsDisposed && placeholder.DocumentPath == null)
                {
                    placeholder.State.Disabled = true;
                    placeholder.Close();
                }
                if (imported) AddTraffic("MBP imported (read polling enabled): " + Path.GetFileName(file) +
                    ". Cached values, aliases and supported mixed formats loaded; some chart/color/font styling is not imported. Save as .mpdoc.");
                RememberFile(file);
            }
            catch (Exception ex)
            {
                LocalMessageBox.Show(this, ex.Message, "Open Data Window");
            }
        }
    }
}

