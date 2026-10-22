using System;
using System.IO;

namespace ModbusGetFree
{
    /// <summary>
    /// 通过 Excel COM 创建并写入日志工作簿；停止时释放引用，保留工作簿供用户保存。
    /// </summary>
    public sealed class ExcelLog : IDisposable
    {
        dynamic app;
        dynamic book;
        dynamic sheet;
        int row = 2;

        public ExcelLog(DataWindow doc)
        {
            Type excel = Type.GetTypeFromProgID("Excel.Application");
            if (excel == null)
            {
                throw new InvalidOperationException("Microsoft Excel is not installed.");
            }

            try
            {
                app = Activator.CreateInstance(excel);
                book = app.Workbooks.Add();
                sheet = book.Worksheets[1];
                sheet.Name = "RTU Log";
                app.Visible = true;
                object[,] headers = new object[1, doc.StorageQuantity + 3];
                headers[0, 0] = "Timestamp";
                headers[0, 1] = "Slave";
                headers[0, 2] = "Function";
                for (int i = 0; i < doc.StorageQuantity; i++)
                {
                    headers[0, i + 3] = "Address " + doc.DisplayAddress(i);
                }

                sheet.Range[sheet.Cells[1, 1], sheet.Cells[1, doc.StorageQuantity + 3]].Value2 = headers;
            }
            catch
            {
                if (app != null)
                {
                    try
                    {
                        app.Quit();
                    }
                    catch
                    {
                    }
                }

                Dispose();
                throw;
            }
        }

        public void Append(DataWindow doc)
        {
            if (row > 1048576)
            {
                throw new IOException("Excel worksheet is full.");
            }

            object[,] data = new object[1, doc.StorageQuantity + 3];
            data[0, 0] = DateTime.Now.ToString("o");
            data[0, 1] = doc.State.Unit;
            data[0, 2] = doc.State.Function;
            for (int i = 0; i < doc.StorageQuantity; i++)
            {
                data[0, i + 3] = doc.State.Values[i];
            }

            sheet.Range[sheet.Cells[row, 1], sheet.Cells[row, doc.StorageQuantity + 3]].Value2 = data;
            row++;
        }

        public void Dispose()
        {
            foreach (object o in new object[]
            {
                sheet,
                book,
                app
            }

            )
            {
                if (o != null && System.Runtime.InteropServices.Marshal.IsComObject(o))
                {
                    System.Runtime.InteropServices.Marshal.FinalReleaseComObject(o);
                }
            }

            sheet = book = app = null;
        }
    }
}
