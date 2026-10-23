using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace ModbusGetFree
{
    /// <summary>
    /// 数据导出及文本/Excel 日志启停。
    /// </summary>
    public partial class DataWindow : Form
    {
        public void Export()
        {
            using (var d = new SaveFileDialog
            {
                Filter = "CSV|*.csv",
                FileName = "registers.csv"
            }

            )
            {
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        File.WriteAllLines(d.FileName, CsvLines(), System.Text.Encoding.UTF8);
                    }
                    catch (Exception ex)
                    {
                        LocalMessageBox.Show(this, ex.Message);
                    }
                }
            }
        }

        string[] CsvLines()
        {
            var lines = new List<string>
            {
                "ProtocolAddress,Name,RawUInt16,DisplayValue"
            };

            for (int i = 0; i < State.Values.Length; i++)
            {
                lines.Add((State.Address + (EnronActive ? i / 2 : i)) + ",\""
                    + (State.Names[i] ?? "").Replace("\"", "\"\"") + "\"," + State.Values[i] + ",\""
                    + FormatGroup(i) + "\"");
            }

            return lines.ToArray();
        }

        public void ToggleLog()
        {
            if (csvLog != null)
            {
                csvLog = null;
                lastMessage = "Data logging stopped";
                UpdateSummary();
                return;
            }

            using (var d = new SaveFileDialog
            {
                Filter = "CSV|*.csv",
                FileName = "modbus-log.csv"
            }

            )
            {
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        File.WriteAllText(d.FileName, "Timestamp,Slave,Function,Address,Name,RawUInt16\r\n",
                             System.Text.Encoding.UTF8);
                        csvLog = d.FileName;
                        lastMessage = "Logging: " + Path.GetFileName(csvLog);
                        UpdateSummary();
                    }
                    catch (Exception ex)
                    {
                        LocalMessageBox.Show(this, ex.Message);
                    }
                }
            }
        }

        void AppendLog()
        {
            var lines = new List<string>();
            string now = DateTime.Now.ToString("o");
            for (int i = 0; i < StorageQuantity; i++)
            {
                lines.Add(now + "," + State.Unit + "," + State.Function + ","
                    + (State.Address + (EnronActive ? i / 2 : i)) + ",\"" + (State.Names[i] ?? "").Replace("\"",
                     "\"\"") + "\"," + State.Values[i]);
            }

            File.AppendAllLines(csvLog, lines, System.Text.Encoding.UTF8);
        }

        public void StartTextLog()
        {
            if (csvLog == null)
            {
                ToggleLog();
            }
        }

        public void StopTextLog()
        {
            csvLog = null;
            lastMessage = "Logging Off";
            UpdateSummary();
        }

        public void StartExcelLog()
        {
            if (excelLog != null)
            {
                return;
            }

            excelLog = new ExcelLog(this);
            FormClosed += (s, e) => StopExcelLog();
            lastMessage = "Excel logging active";
            UpdateSummary();
        }

        public void StopExcelLog()
        {
            if (excelLog != null)
            {
                excelLog.Dispose();
                excelLog = null;
            }

            lastMessage = "Excel Logging Off";
            if (!IsDisposed)
            {
                UpdateSummary();
            }
        }
    }
}
