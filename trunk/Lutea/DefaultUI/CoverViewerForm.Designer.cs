namespace Gageas.Lutea.DefaultUI
{
    partial class CoverViewerForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // CoverViewer
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.ClientSize = new System.Drawing.Size(387, 399);
            this.ControlBox = false;
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.MinimumSize = new System.Drawing.Size(150, 150);
            this.Name = "CoverViewer";
            this.Opacity = 0D;
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "CoverViewer";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.CoverViewer_FormClosing);
            this.Load += new System.EventHandler(this.CoverViewer_Load);
            this.Click += new System.EventHandler(this.CoverViewer_Click);
            this.Paint += new System.Windows.Forms.PaintEventHandler(this.CoverViewer_Paint);
            this.Resize += new System.EventHandler(this.CoverViewer_Resize);
            this.ResumeLayout(false);

        }

        #endregion
    }
}