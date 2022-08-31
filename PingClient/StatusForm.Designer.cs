namespace PingClient
{
    partial class StatusForm
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(StatusForm));
            this.lvNodes = new System.Windows.Forms.ListView();
            this.columnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader2 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.labPingServiceWorked = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.labStatus = new System.Windows.Forms.Label();
            this.timerPingServiceLiveTimeout = new System.Windows.Forms.Timer(this.components);
            this.SuspendLayout();
            // 
            // lvNodes
            // 
            this.lvNodes.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lvNodes.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader1,
            this.columnHeader2});
            this.lvNodes.FullRowSelect = true;
            this.lvNodes.HideSelection = false;
            this.lvNodes.Location = new System.Drawing.Point(12, 44);
            this.lvNodes.MultiSelect = false;
            this.lvNodes.Name = "lvNodes";
            this.lvNodes.ShowItemToolTips = true;
            this.lvNodes.Size = new System.Drawing.Size(182, 160);
            this.lvNodes.Sorting = System.Windows.Forms.SortOrder.Ascending;
            this.lvNodes.TabIndex = 4;
            this.lvNodes.UseCompatibleStateImageBehavior = false;
            this.lvNodes.View = System.Windows.Forms.View.Details;
            // 
            // columnHeader1
            // 
            this.columnHeader1.Text = "Computer IP";
            this.columnHeader1.Width = 80;
            // 
            // columnHeader2
            // 
            this.columnHeader2.Text = "Status";
            this.columnHeader2.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.columnHeader2.Width = 80;
            // 
            // labPingServiceWorked
            // 
            this.labPingServiceWorked.AutoSize = true;
            this.labPingServiceWorked.Location = new System.Drawing.Point(125, 26);
            this.labPingServiceWorked.Name = "labPingServiceWorked";
            this.labPingServiceWorked.Size = new System.Drawing.Size(16, 15);
            this.labPingServiceWorked.TabIndex = 8;
            this.labPingServiceWorked.Text = "...";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 25);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(107, 15);
            this.label2.TabIndex = 6;
            this.label2.Text = "Ping service status:";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 10);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(112, 15);
            this.label1.TabIndex = 7;
            this.label1.Text = "Link to event server:";
            // 
            // labStatus
            // 
            this.labStatus.AutoSize = true;
            this.labStatus.Location = new System.Drawing.Point(130, 10);
            this.labStatus.Name = "labStatus";
            this.labStatus.Size = new System.Drawing.Size(16, 15);
            this.labStatus.TabIndex = 5;
            this.labStatus.Text = "...";
            // 
            // timerPingServiceLiveTimeout
            // 
            this.timerPingServiceLiveTimeout.Interval = 10000;
            this.timerPingServiceLiveTimeout.Tick += new System.EventHandler(this.timerPingServiceLiveTimeout_Tick);
            // 
            // StatusForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(206, 215);
            this.Controls.Add(this.lvNodes);
            this.Controls.Add(this.labPingServiceWorked);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.labStatus);
            this.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "StatusForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Link NET status";
            this.Load += new System.EventHandler(this.StatusForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.ColumnHeader columnHeader1;
        private System.Windows.Forms.ColumnHeader columnHeader2;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        public System.Windows.Forms.ListView lvNodes;
        public System.Windows.Forms.Label labPingServiceWorked;
        public System.Windows.Forms.Label labStatus;
        private System.Windows.Forms.Timer timerPingServiceLiveTimeout;
    }
}