
using Client.VisualComponents;

namespace Client;

sealed partial class Main
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
        components = new System.ComponentModel.Container();
        ipTextbox = new TextBox();
        label1 = new Label();
        label2 = new Label();
        portTextbox = new TextBox();
        connectButton = new Button();
        isClient = new RadioButton();
        isHost = new RadioButton();
        panel1 = new Panel();
        logList = new DoubleBufferedListBox();
        toolTip1 = new ToolTip(components);
        playerDataGrid = new DataGridView();
        panel1.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)playerDataGrid).BeginInit();
        SuspendLayout();
        // 
        // ipTextbox
        // 
        ipTextbox.Location = new Point(560, 12);
        ipTextbox.Name = "ipTextbox";
        ipTextbox.Size = new Size(163, 23);
        ipTextbox.TabIndex = 0;
        // 
        // label1
        // 
        label1.AutoSize = true;
        label1.Location = new Point(537, 15);
        label1.Name = "label1";
        label1.Size = new Size(17, 15);
        label1.TabIndex = 1;
        label1.Text = "IP";
        label1.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // label2
        // 
        label2.AutoSize = true;
        label2.Location = new Point(729, 15);
        label2.Name = "label2";
        label2.Size = new Size(29, 15);
        label2.TabIndex = 2;
        label2.Text = "Port";
        label2.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // portTextbox
        // 
        portTextbox.Location = new Point(764, 12);
        portTextbox.Name = "portTextbox";
        portTextbox.Size = new Size(62, 23);
        portTextbox.TabIndex = 3;
        // 
        // connectButton
        // 
        connectButton.Location = new Point(537, 41);
        connectButton.Name = "connectButton";
        connectButton.Size = new Size(413, 33);
        connectButton.TabIndex = 4;
        connectButton.Text = "Connect";
        connectButton.UseVisualStyleBackColor = true;
        connectButton.Click += connectButton_Click;
        // 
        // isClient
        // 
        isClient.AutoSize = true;
        isClient.Location = new Point(59, 3);
        isClient.Name = "isClient";
        isClient.Size = new Size(55, 19);
        isClient.TabIndex = 7;
        isClient.Text = "Client";
        isClient.UseVisualStyleBackColor = true;
        isClient.CheckedChanged += isClient_CheckedChanged;
        // 
        // isHost
        // 
        isHost.AutoSize = true;
        isHost.Location = new Point(3, 3);
        isHost.Name = "isHost";
        isHost.Size = new Size(50, 19);
        isHost.TabIndex = 7;
        isHost.Text = "Host";
        isHost.UseVisualStyleBackColor = true;
        isHost.CheckedChanged += isHost_CheckedChanged;
        // 
        // panel1
        // 
        panel1.Controls.Add(isHost);
        panel1.Controls.Add(isClient);
        panel1.Location = new Point(832, 12);
        panel1.Name = "panel1";
        panel1.Size = new Size(118, 25);
        panel1.TabIndex = 8;
        // 
        // logList
        // 
        logList.BackColor = Color.Black;
        logList.DrawMode = DrawMode.OwnerDrawFixed;
        logList.Font = new Font("Yu Gothic UI", 9F);
        logList.ForeColor = Color.White;
        logList.FormattingEnabled = true;
        logList.Location = new Point(537, 80);
        logList.Name = "logList";
        logList.ScrollAlwaysVisible = true;
        logList.SelectionMode = SelectionMode.None;
        logList.Size = new Size(413, 356);
        logList.TabIndex = 9;
        logList.DrawItem += logList_DrawItem;
        // 
        // playerDataGrid
        // 
        playerDataGrid.AllowUserToAddRows = false;
        playerDataGrid.AllowUserToDeleteRows = false;
        playerDataGrid.AllowUserToResizeRows = false;
        playerDataGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        playerDataGrid.EditMode = DataGridViewEditMode.EditProgrammatically;
        playerDataGrid.Location = new Point(12, 12);
        playerDataGrid.Name = "playerDataGrid";
        playerDataGrid.ReadOnly = true;
        playerDataGrid.Size = new Size(519, 424);
        playerDataGrid.TabIndex = 10;
        // 
        // Main
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(962, 448);
        Controls.Add(playerDataGrid);
        Controls.Add(logList);
        Controls.Add(panel1);
        Controls.Add(portTextbox);
        Controls.Add(ipTextbox);
        Controls.Add(label2);
        Controls.Add(label1);
        Controls.Add(connectButton);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        Name = "Main";
        Text = "Factorio Discord Proximity Audio";
        FormClosing += Main_FormClosing;
        Load += Main_Load;
        panel1.ResumeLayout(false);
        panel1.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)playerDataGrid).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private TextBox ipTextbox;
    private Label label1;
    private Label label2;
    private TextBox portTextbox;
    private Button connectButton;
    private RadioButton isHost;
    private RadioButton isClient;
    private Panel panel1;
    private DoubleBufferedListBox logList;
    private ToolTip toolTip1;
    private DataGridView playerDataGrid;
}