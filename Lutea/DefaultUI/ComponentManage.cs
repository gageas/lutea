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
                var info = component.GetType().GetCustomAttributes(typeof(LuteaComponentInfo), false);
                if (info.Length > 0)
                {
                    this.comboBox1.Items.Add(info[0]);
//                    LuteaComponentInfo cominfo = (LuteaComponentInfo)info[0];
//                    Logger.Log(cominfo.name);
                }
            }
            this.comboBox1.SelectedIndex = 0;
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox1.SelectedItem != null)
            {
                LuteaComponentInterface component = lcomponents[this.comboBox1.SelectedIndex];
                var pref = component.GetPreferenceObject();
                this.propertyGrid1.SelectedObject = component.GetPreferenceObject();
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            LuteaComponentInterface component = lcomponents[this.comboBox1.SelectedIndex];
            component.SetPreferenceObject(this.propertyGrid1.SelectedObject);
        }

        private void Close_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
