using System;
using System.ComponentModel;
using System.Windows.Forms;
using System.Windows.Forms.Design;

namespace FG.CheckoutAndBuild2.Controls
{
    public class RuntimeUIServiceProvider : ITypeDescriptorContext
    {
        public IContainer Container => null;

        public object Instance => null;

        public PropertyDescriptor PropertyDescriptor => null;

        object IServiceProvider.GetService(Type serviceType)
        {
            if (serviceType == typeof(IWindowsFormsEditorService))
                return new EditorService();
            return null;
        }

        public void OnComponentChanged()
        {}

        public bool OnComponentChanging()
        {
            return true;
        }

        private class EditorService : IWindowsFormsEditorService
        {
            public void DropDownControl(Control control)
            {
                Form form = new Form();
                form.Controls.Add(control);
                form.Height = control.Height + 40;
                form.Width = control.Width + 20;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.StartPosition = FormStartPosition.CenterParent;
                int num = (int)form.ShowDialog();
            }

            public void CloseDropDown()
            {
            }

            public DialogResult ShowDialog(Form dialog)
            {
                return dialog.ShowDialog();
            }
        }
    }
}