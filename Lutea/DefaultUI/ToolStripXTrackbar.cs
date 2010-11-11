using System.Windows.Forms;

namespace Gageas.Lutea.DefaultUI
{
    [System.Windows.Forms.Design.ToolStripItemDesignerAvailability(
       System.Windows.Forms.Design.ToolStripItemDesignerAvailability.ToolStrip |
       System.Windows.Forms.Design.ToolStripItemDesignerAvailability.StatusStrip)]
    public class ToolStripXTrackbar : ToolStripControlHost
    {
        public ToolStripXTrackbar()
            : base(new XTrackBar())
        {
            XTrackBar self = GetControl;
            self.OnScroll += new Core.Controller.VOIDVOID(() => this.onScroll.Invoke());
//            self.DropDownStyle = ComboBoxStyle.DropDownList;
        }
        public XTrackBar GetControl
        {
            get
            {
                return (XTrackBar)this.Control;
            }
        }
        public event Core.Controller.VOIDVOID onScroll;
    }
}