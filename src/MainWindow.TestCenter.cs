using System.Windows.Forms;
namespace ModbusGetFree
{
    public partial class MainWindow
    {
        TestCenterWindow testCenterForm;
        void TestCenter()
        {
            if (testCenterForm == null || testCenterForm.IsDisposed)
                testCenterForm = new TestCenterWindow(this);
            ShowToolWindow(testCenterForm);
        }
    }
}
