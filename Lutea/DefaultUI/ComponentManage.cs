﻿using System;
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
        private LuteaComponentInterface selectedComponent;
        private LuteaComponentInterface[] lcomponents;
        private Dictionary<LuteaComponentInterface, object> prefs = new Dictionary<LuteaComponentInterface, object>();
        private Dictionary<LuteaComponentInterface, bool> changed = new Dictionary<LuteaComponentInterface, bool>();
        public ComponentManager()
        {
            InitializeComponent();
            button1.Enabled = false;
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
            initPrefPages();
            this.listView1.Items[0].Selected = true;
        }

        private void initPrefPages()
        {
            foreach (var component in lcomponents)
            {
                prefs[component] = component.GetPreferenceObject();
                changed[component] = false;
            }
        }
        
        private void button1_Click(object sender, EventArgs e)
        {
            button1.Enabled = false;
            Cursor = Cursors.WaitCursor;

            // 設定を格納
            foreach (var component in lcomponents)
            {
                if (changed[component])
                {
                    component.SetPreferenceObject(prefs[component]);
                }
            } 
            
            // Preferenceページを初期化
            initPrefPages();
            this.propertyGrid1.SelectedObject = prefs[selectedComponent];
            Cursor = Cursors.Default;
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count > 0)
            {
                selectedComponent = lcomponents[listView1.SelectedIndices[0]];
                groupBox1.Text = listView1.SelectedItems[0].ToolTipText;
                this.propertyGrid1.SelectedObject = prefs[selectedComponent];
            }
        }

        private void propertyGrid1_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            changed[selectedComponent] = true;
            button1.Enabled = true;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
        {

        }

        private void resetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                var propName = this.propertyGrid1.SelectedGridItem.PropertyDescriptor.Name;
                var prop = this.propertyGrid1.SelectedObject.GetType().GetProperty(propName);
                var defaultValueAttr = (DefaultValueAttribute)(prop.GetCustomAttributes(typeof(DefaultValueAttribute), true).First());
                prop.SetValue(this.propertyGrid1.SelectedObject, defaultValueAttr.Value, null);
            }
            catch (Exception) { }
            finally {
                this.propertyGrid1.Refresh();
                changed[selectedComponent] = true;
                button1.Enabled = true;
            }
        }
    }
}
