using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using Gageas.Lutea;
using Gageas.Lutea.Core;

namespace Gageas.Lutea.DefaultUI
{
    public partial class ComponentManager : Form
    {
        private LuteaComponentInterface[] lcomponents;
        public ComponentManager()
        {
            InitializeComponent();
            lcomponents = Controller.GetComponents();
            foreach (var component in lcomponents)
            {
                LuteaComponentInfo[] info = (LuteaComponentInfo[])component.GetType().GetCustomAttributes(typeof(LuteaComponentInfo), false);
                if (info.Length > 0)
                {
                    var item = new ListViewItem(info[0].name);
                    item.ToolTipText = info[0].ToString();
                    this.listView1.Items.Add(item);
                }
            }
            this.listView1.Items[0].Selected = true;
        }
        
        private void button1_Click(object sender, EventArgs e)
        {
            LuteaComponentInterface component = lcomponents[listView1.SelectedIndices[0]];
            component.SetPreferenceObject(this.propertyGrid1.SelectedObject);
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count > 0)
            {
                LuteaComponentInterface component = lcomponents[listView1.SelectedIndices[0]];
                var pref = component.GetPreferenceObject();
                groupBox1.Text = listView1.SelectedItems[0].ToolTipText;
                this.propertyGrid1.SelectedObject = component.GetPreferenceObject();
            }
        }
    }
}
