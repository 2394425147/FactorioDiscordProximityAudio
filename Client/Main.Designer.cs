
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
        label1 = new Label();
        label2 = new Label();
        connectButton = new Button();
        isClient = new RadioButton();
        isHost = new RadioButton();
        panel1 = new Panel();
        toolTip1 = new ToolTip(components);
        clientPositionBindingSource = new BindingSource(components);
        ipTextbox = new PastableTextBox();
        portTextbox = new PastableTextBox();
        logTextbox = new RichTextBox();
        panel1.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)clientPositionBindingSource).BeginInit();
        SuspendLayout();
        // 
        // label1
        // 
        label1.AutoSize = true;
        label1.Location = new Point(12, 15);
        label1.Name = "label1";
        label1.Size = new Size(17, 15);
        label1.TabIndex = 1;
        label1.Text = "IP";
        label1.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // label2
        // 
        label2.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        label2.AutoSize = true;
        label2.Location = new Point(337, 15);
        label2.Name = "label2";
        label2.Size = new Size(29, 15);
        label2.TabIndex = 2;
        label2.Text = "Port";
        label2.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // connectButton
        // 
        connectButton.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        connectButton.Location = new Point(12, 41);
        connectButton.Name = "connectButton";
        connectButton.Size = new Size(544, 33);
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
        panel1.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        panel1.Controls.Add(isHost);
        panel1.Controls.Add(isClient);
        panel1.Location = new Point(439, 12);
        panel1.Name = "panel1";
        panel1.Size = new Size(117, 25);
        panel1.TabIndex = 8;
        // 
        // clientPositionBindingSource
        // 
        clientPositionBindingSource.DataSource = typeof(Models.ClientPosition);
        // 
        // ipTextbox
        // 
        ipTextbox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        ipTextbox.Location = new Point(35, 12);
        ipTextbox.Name = "ipTextbox";
        ipTextbox.Size = new Size(296, 23);
        ipTextbox.TabIndex = 11;
        ipTextbox.Pasted += AddressPasted;
        ipTextbox.TextChanged += OnIPTextChanged;
        // 
        // portTextbox
        // 
        portTextbox.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        portTextbox.Location = new Point(372, 12);
        portTextbox.Name = "portTextbox";
        portTextbox.Size = new Size(61, 23);
        portTextbox.TabIndex = 12;
        portTextbox.Pasted += AddressPasted;
        // 
        // logTextbox
        // 
        logTextbox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        logTextbox.BackColor = Color.Black;
        logTextbox.Location = new Point(12, 80);
        logTextbox.Name = "logTextbox";
        logTextbox.ReadOnly = true;
        logTextbox.ScrollBars = RichTextBoxScrollBars.ForcedVertical;
        logTextbox.Size = new Size(544, 356);
        logTextbox.TabIndex = 13;
        logTextbox.Text = "";
        // 
        // Main
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(568, 448);
        Controls.Add(logTextbox);
        Controls.Add(portTextbox);
        Controls.Add(ipTextbox);
        Controls.Add(panel1);
        Controls.Add(label2);
        Controls.Add(label1);
        Controls.Add(connectButton);
        DoubleBuffered = true;
        MinimumSize = new Size(384, 256);
        Name = "Main";
        Text = "Factorio Discord Proximity Audio";
        Load += Main_Load;
        panel1.ResumeLayout(false);
        panel1.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)clientPositionBindingSource).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion
    private Label label1;
    private Label label2;
    private Button connectButton;
    private RadioButton isHost;
    private RadioButton isClient;
    private Panel panel1;
    private ToolTip toolTip1;
    private BindingSource clientPositionBindingSource;
    private PastableTextBox ipTextbox;
    private PastableTextBox portTextbox;
    private RichTextBox logTextbox;
}
