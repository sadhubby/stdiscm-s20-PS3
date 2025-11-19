namespace ConsumerGUI
{
    partial class Form1
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
            this.flowPanelVideos = new System.Windows.Forms.FlowLayoutPanel();
            this.statuslabel = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // flowPanelVideos
            // 
            this.flowPanelVideos.Dock = System.Windows.Forms.DockStyle.Top;
            this.flowPanelVideos.Location = new System.Drawing.Point(0, 0);
            this.flowPanelVideos.Name = "flowPanelVideos";
            this.flowPanelVideos.Size = new System.Drawing.Size(800, 300);
            this.flowPanelVideos.TabIndex = 0;
            this.flowPanelVideos.Paint += new System.Windows.Forms.PaintEventHandler(this.flowPanelVideos_Paint);
            // 
            // statuslabel
            // 
            this.statuslabel.AutoSize = true;
            this.statuslabel.Dock = System.Windows.Forms.DockStyle.Right;
            this.statuslabel.Location = new System.Drawing.Point(762, 300);
            this.statuslabel.Name = "statuslabel";
            this.statuslabel.Size = new System.Drawing.Size(38, 13);
            this.statuslabel.TabIndex = 1;
            this.statuslabel.Text = "Ready";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.statuslabel);
            this.Controls.Add(this.flowPanelVideos);
            this.Name = "Form1";
            this.Text = "Form1";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.FlowLayoutPanel flowPanelVideos;
        private System.Windows.Forms.Label statuslabel;
    }
}

