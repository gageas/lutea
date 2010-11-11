using System.Windows.Forms;

namespace Gageas.Lutea.DefaultUI
{
    [System.Windows.Forms.Design.ToolStripItemDesignerAvailability(
       System.Windows.Forms.Design.ToolStripItemDesignerAvailability.ToolStrip |
       System.Windows.Forms.Design.ToolStripItemDesignerAvailability.StatusStrip)]
    public class ToolStripComboBox : ToolStripControlHost
    {
        public ToolStripComboBox()
            : base(new ComboBox())
        {
            ComboBox self = GetControl;
            self.DropDownStyle = ComboBoxStyle.DropDownList;
        }
        public ComboBox GetControl
        {
            get
            {
                return (ComboBox)this.Control;
            }
        }
    }
}