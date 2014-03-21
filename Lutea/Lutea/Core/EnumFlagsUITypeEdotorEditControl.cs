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
            var list = new List<Tuple<string, bool>>();
            foreach (var val in values)
            {
                list.Add(new Tuple<string,bool>(Enum.GetName(type, val), (initialValue & (int)val) != 0));
            }
            foreach (var t in list.OrderBy(_ => _.Item1))
            {
                checkedListBox1.Items.Add(t.Item1, t.Item2);
            }
            this.Height = checkedListBox1.GetItemRectangle(0).Height * checkedListBox1.Items.Count;
        }
    }
}
