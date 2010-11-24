namespace Gageas.Lutea.DefaultUI
{
    partial class XTrackBar
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // XTrackBar
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.MinimumSize = new System.Drawing.Size(70, 20);
            this.Name = "XTrackBar";
            this.Paint += new System.Windows.Forms.PaintEventHandler(this.XTrackBar_Paint);
            this.MouseDown += new System.Windows.Forms.MouseEventHandler(this.XTrackBar_MouseDown);
            this.MouseLeave += new System.EventHandler(this.XTrackBar_MouseLeave);
            this.MouseMove += new System.Windows.Forms.MouseEventHandler(this.XTrackBar_MouseMove);
            this.MouseUp += new System.Windows.Forms.MouseEventHandler(this.XTrackBar_MouseUp);
            this.Resize += new System.EventHandler(this.XTrackBar_Resize);
            this.ResumeLayout(false);

        }

        #endregion
    }
}
