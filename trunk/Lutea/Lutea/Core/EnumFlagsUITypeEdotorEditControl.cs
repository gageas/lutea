using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Gageas.Lutea.Core
{
    public partial class EnumFlagsUITypeEdotorEditControl : UserControl
    {
        private int initialValue;
        private System.Type type;
        public int Value
        {
            get
            {
                int tmp = 0;
                foreach(var item in checkedListBox1.CheckedItems){
                    tmp |= (int)Enum.Parse(type, item.ToString());
                }
                return tmp;
            }
        }

        public EnumFlagsUITypeEdotorEditControl(Type enumType, int initialValue)
        {
            this.type = enumType;
            this.initialValue = initialValue;
            InitializeComponent();
        }

        private void FileTypesUIEditorControl_Load(object sender, EventArgs e)
        {
            var values = Enum.GetValues(type);
            foreach (var val in values)
            {
                checkedListBox1.Items.Add(Enum.GetName(type, val), (initialValue & (int)val) != 0);
            }
        }
    }
}
