namespace BNet.WebSocket.Server.Sample
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
            this.button_Start = new System.Windows.Forms.Button();
            this.button_Stop = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.label_Status = new System.Windows.Forms.Label();
            this.textBox_Hostname = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.textBox_ReceivedMessage = new System.Windows.Forms.TextBox();
            this.textBox_SendAll = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.button_SendAll = new System.Windows.Forms.Button();
            this.button_SendRoom = new System.Windows.Forms.Button();
            this.label6 = new System.Windows.Forms.Label();
            this.textBox_SendRoom = new System.Windows.Forms.TextBox();
            this.textBox_Room = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.label_CountClients = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // button_Start
            // 
            this.button_Start.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.button_Start.Location = new System.Drawing.Point(45, 350);
            this.button_Start.Name = "button_Start";
            this.button_Start.Size = new System.Drawing.Size(105, 41);
            this.button_Start.TabIndex = 0;
            this.button_Start.Text = "Start";
            this.button_Start.UseVisualStyleBackColor = true;
            this.button_Start.Click += new System.EventHandler(this.button_Start_Click);
            // 
            // button_Stop
            // 
            this.button_Stop.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.button_Stop.Location = new System.Drawing.Point(176, 350);
            this.button_Stop.Name = "button_Stop";
            this.button_Stop.Size = new System.Drawing.Size(105, 41);
            this.button_Stop.TabIndex = 1;
            this.button_Stop.Text = "Stop";
            this.button_Stop.UseVisualStyleBackColor = true;
            this.button_Stop.Click += new System.EventHandler(this.button_Stop_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(44, 404);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(93, 25);
            this.label1.TabIndex = 2;
            this.label1.Text = "Status : ";
            // 
            // label_Status
            // 
            this.label_Status.AutoSize = true;
            this.label_Status.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label_Status.Location = new System.Drawing.Point(143, 404);
            this.label_Status.Name = "label_Status";
            this.label_Status.Size = new System.Drawing.Size(57, 25);
            this.label_Status.TabIndex = 3;
            this.label_Status.Text = "Stop";
            // 
            // textBox_Hostname
            // 
            this.textBox_Hostname.Location = new System.Drawing.Point(46, 128);
            this.textBox_Hostname.Multiline = true;
            this.textBox_Hostname.Name = "textBox_Hostname";
            this.textBox_Hostname.Size = new System.Drawing.Size(235, 206);
            this.textBox_Hostname.TabIndex = 4;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(41, 100);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(109, 25);
            this.label2.TabIndex = 5;
            this.label2.Text = "Hostname";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(24, 25);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(292, 37);
            this.label3.TabIndex = 6;
            this.label3.Text = "Websocket Server";
            // 
            // textBox_ReceivedMessage
            // 
            this.textBox_ReceivedMessage.Location = new System.Drawing.Point(380, 62);
            this.textBox_ReceivedMessage.Multiline = true;
            this.textBox_ReceivedMessage.Name = "textBox_ReceivedMessage";
            this.textBox_ReceivedMessage.Size = new System.Drawing.Size(669, 171);
            this.textBox_ReceivedMessage.TabIndex = 7;
            // 
            // textBox_SendAll
            // 
            this.textBox_SendAll.Location = new System.Drawing.Point(380, 276);
            this.textBox_SendAll.Name = "textBox_SendAll";
            this.textBox_SendAll.Size = new System.Drawing.Size(408, 26);
            this.textBox_SendAll.TabIndex = 8;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label4.Location = new System.Drawing.Point(375, 34);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(195, 25);
            this.label4.TabIndex = 9;
            this.label4.Text = "Received Message";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.Location = new System.Drawing.Point(375, 248);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(225, 25);
            this.label5.TabIndex = 10;
            this.label5.Text = "Message To All Client";
            // 
            // button_SendAll
            // 
            this.button_SendAll.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.button_SendAll.Location = new System.Drawing.Point(814, 261);
            this.button_SendAll.Name = "button_SendAll";
            this.button_SendAll.Size = new System.Drawing.Size(235, 41);
            this.button_SendAll.TabIndex = 11;
            this.button_SendAll.Text = "Send All";
            this.button_SendAll.UseVisualStyleBackColor = true;
            this.button_SendAll.Click += new System.EventHandler(this.button_SendAll_Click);
            // 
            // button_SendRoom
            // 
            this.button_SendRoom.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.button_SendRoom.Location = new System.Drawing.Point(944, 335);
            this.button_SendRoom.Name = "button_SendRoom";
            this.button_SendRoom.Size = new System.Drawing.Size(105, 41);
            this.button_SendRoom.TabIndex = 14;
            this.button_SendRoom.Text = "Send";
            this.button_SendRoom.UseVisualStyleBackColor = true;
            this.button_SendRoom.Click += new System.EventHandler(this.button_SendRoom_Click);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label6.Location = new System.Drawing.Point(375, 322);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(255, 25);
            this.label6.TabIndex = 13;
            this.label6.Text = "Message To Client Room";
            // 
            // textBox_SendRoom
            // 
            this.textBox_SendRoom.Location = new System.Drawing.Point(380, 350);
            this.textBox_SendRoom.Name = "textBox_SendRoom";
            this.textBox_SendRoom.Size = new System.Drawing.Size(408, 26);
            this.textBox_SendRoom.TabIndex = 12;
            // 
            // textBox_Room
            // 
            this.textBox_Room.Location = new System.Drawing.Point(811, 350);
            this.textBox_Room.Name = "textBox_Room";
            this.textBox_Room.Size = new System.Drawing.Size(127, 26);
            this.textBox_Room.TabIndex = 15;
            this.textBox_Room.Text = "MyRoom";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label7.Location = new System.Drawing.Point(809, 322);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(129, 25);
            this.label7.TabIndex = 16;
            this.label7.Text = "Room Name";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Font = new System.Drawing.Font("Microsoft Sans Serif", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label8.Location = new System.Drawing.Point(384, 382);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(333, 20);
            this.label8.TabIndex = 17;
            this.label8.Text = "Example : ws://localhost:8080?room=MyRoom";
            // 
            // label_CountClients
            // 
            this.label_CountClients.AutoSize = true;
            this.label_CountClients.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label_CountClients.Location = new System.Drawing.Point(375, 416);
            this.label_CountClients.Name = "label_CountClients";
            this.label_CountClients.Size = new System.Drawing.Size(221, 25);
            this.label_CountClients.TabIndex = 18;
            this.label_CountClients.Text = "Connected Clients : 0";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1075, 450);
            this.Controls.Add(this.label_CountClients);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.textBox_Room);
            this.Controls.Add(this.button_SendRoom);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.textBox_SendRoom);
            this.Controls.Add(this.button_SendAll);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.textBox_SendAll);
            this.Controls.Add(this.textBox_ReceivedMessage);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.textBox_Hostname);
            this.Controls.Add(this.label_Status);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.button_Stop);
            this.Controls.Add(this.button_Start);
            this.Name = "Form1";
            this.Text = "Form1";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button button_Start;
        private System.Windows.Forms.Button button_Stop;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label_Status;
        private System.Windows.Forms.TextBox textBox_Hostname;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textBox_ReceivedMessage;
        private System.Windows.Forms.TextBox textBox_SendAll;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Button button_SendAll;
        private System.Windows.Forms.Button button_SendRoom;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox textBox_SendRoom;
        private System.Windows.Forms.TextBox textBox_Room;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label_CountClients;
    }
}

