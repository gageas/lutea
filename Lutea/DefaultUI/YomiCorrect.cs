using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Gageas.Lutea.DefaultUI
{
    public partial class YomiCorrect : Form
    {
        private Yomigana yomigana;
        private string src;
        public YomiCorrect(string src, Yomigana yomigana)
        {
            this.yomigana = yomigana;
            this.src = src;
            InitializeComponent();
            label1.Text = "[" + yomigana.GetLeadingChars(src) + "] " + src;
            textBox1.Text = yomigana.GetFirst(src).ToString();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            yomigana.Correct(src, textBox1.Text);
            this.Close();
        }
    }
}
