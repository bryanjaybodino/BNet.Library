using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using BNet.FTPServer;


namespace BNet.FTPServer.Sample
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        Commands commands = new Commands();
        private void Form1_Load(object sender, EventArgs e)
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus == OperationalStatus.Up)
                {
                    IPInterfaceProperties properties = ni.GetIPProperties();
                    foreach (UnicastIPAddressInformation ip in properties.UnicastAddresses)
                    {
                        textBox_Hostname.AppendText("\r\nftp://" + ip.Address.ToString() + ":2022");
                    }
                }
            }
            //commands.UserCredentials.Add("bryan", "1234");
            //commands.UserCredentials.Add("bodino", "143");
            //commands.LoadCertificate("C:\\cert.pfx", "");
        }
        private async void button_Start_Click(object sender, EventArgs e)
        {
            label_Status.Text = "Running";
            commands.Setup("C:\\", 2022);
            await commands.StartAsync();
        }

        private async void button_Stop_Click(object sender, EventArgs e)
        {
            label_Status.Text = "Stop";
            await commands.StopAsync();
        }
    }
}
