using System.Windows.Forms;

namespace Gageas.Lutea.DefaultUI
{
    public class ToolStripTrackbar : ToolStripControlHost
    {
        public ToolStripTrackbar()
            : base(new TrackBar())
        {

        }
    }
}