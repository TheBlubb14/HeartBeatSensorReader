using System.ComponentModel.Design;

namespace BleExplorer
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            listBoxAdvertisements = new ListBox();
            buttonStartStopScanning = new Button();
            listBoxServices = new ListBox();
            listBoxCharacteristics = new ListBox();
            progressBarServices = new ProgressBar();
            progressBarAdvertisement = new ProgressBar();
            progressBarCharacteristics = new ProgressBar();
            byteViewer = new ByteViewer();
            progressBarByteViewer = new ProgressBar();
            SuspendLayout();
            // 
            // listBoxAdvertisements
            // 
            listBoxAdvertisements.FormattingEnabled = true;
            listBoxAdvertisements.Location = new Point(12, 12);
            listBoxAdvertisements.Name = "listBoxAdvertisements";
            listBoxAdvertisements.Size = new Size(328, 334);
            listBoxAdvertisements.TabIndex = 0;
            listBoxAdvertisements.SelectedIndexChanged += listBoxAdvertisements_SelectedIndexChanged;
            // 
            // buttonStartStopScanning
            // 
            buttonStartStopScanning.Location = new Point(12, 527);
            buttonStartStopScanning.Name = "buttonStartStopScanning";
            buttonStartStopScanning.Size = new Size(75, 94);
            buttonStartStopScanning.TabIndex = 1;
            buttonStartStopScanning.Text = "Start scanning";
            buttonStartStopScanning.UseVisualStyleBackColor = true;
            buttonStartStopScanning.Click += buttonStartStopScanning_Click;
            // 
            // listBoxServices
            // 
            listBoxServices.FormattingEnabled = true;
            listBoxServices.Location = new Point(346, 12);
            listBoxServices.Name = "listBoxServices";
            listBoxServices.Size = new Size(328, 334);
            listBoxServices.TabIndex = 2;
            listBoxServices.SelectedIndexChanged += listBoxServices_SelectedIndexChanged;
            // 
            // listBoxCharacteristics
            // 
            listBoxCharacteristics.FormattingEnabled = true;
            listBoxCharacteristics.Location = new Point(680, 12);
            listBoxCharacteristics.Name = "listBoxCharacteristics";
            listBoxCharacteristics.Size = new Size(328, 334);
            listBoxCharacteristics.TabIndex = 3;
            listBoxCharacteristics.SelectedIndexChanged += listBoxCharacteristics_SelectedIndexChanged;
            // 
            // progressBarServices
            // 
            progressBarServices.Location = new Point(346, 352);
            progressBarServices.Name = "progressBarServices";
            progressBarServices.Size = new Size(328, 23);
            progressBarServices.TabIndex = 4;
            // 
            // progressBarAdvertisement
            // 
            progressBarAdvertisement.Location = new Point(12, 352);
            progressBarAdvertisement.Name = "progressBarAdvertisement";
            progressBarAdvertisement.Size = new Size(328, 23);
            progressBarAdvertisement.TabIndex = 5;
            // 
            // progressBarCharacteristics
            // 
            progressBarCharacteristics.Location = new Point(680, 352);
            progressBarCharacteristics.Name = "progressBarCharacteristics";
            progressBarCharacteristics.Size = new Size(328, 23);
            progressBarCharacteristics.TabIndex = 6;
            // 
            // byteViewer
            // 
            byteViewer.CellBorderStyle = TableLayoutPanelCellBorderStyle.Inset;
            byteViewer.ColumnCount = 1;
            byteViewer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            byteViewer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            byteViewer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            byteViewer.Location = new Point(1014, 12);
            byteViewer.Name = "byteViewer";
            byteViewer.RowCount = 1;
            byteViewer.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            byteViewer.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            byteViewer.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            byteViewer.Size = new Size(634, 334);
            byteViewer.TabIndex = 7;
            byteViewer.MouseDoubleClick += byteViewer_MouseDoubleClick;
            // 
            // progressBarByteViewer
            // 
            progressBarByteViewer.Location = new Point(1014, 352);
            progressBarByteViewer.Name = "progressBarByteViewer";
            progressBarByteViewer.Size = new Size(634, 23);
            progressBarByteViewer.TabIndex = 8;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1694, 633);
            Controls.Add(progressBarByteViewer);
            Controls.Add(progressBarCharacteristics);
            Controls.Add(progressBarAdvertisement);
            Controls.Add(progressBarServices);
            Controls.Add(listBoxCharacteristics);
            Controls.Add(listBoxServices);
            Controls.Add(buttonStartStopScanning);
            Controls.Add(listBoxAdvertisements);
            Controls.Add(byteViewer);
            Name = "Form1";
            Text = "BleExplorer";
            Load += Form1_Load;
            ResumeLayout(false);
        }

        #endregion

        private ListBox listBoxAdvertisements;
        private Button buttonStartStopScanning;
        private ListBox listBoxServices;
        private ListBox listBoxCharacteristics;
        private ProgressBar progressBarServices;
        private ProgressBar progressBarAdvertisement;
        private ProgressBar progressBarCharacteristics;
        private ByteViewer byteViewer;
        private ProgressBar progressBarByteViewer;
    }
}
