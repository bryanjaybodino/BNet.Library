using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BNet.WebSocket.Server.Sample
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        BNet.WebSocket.Server.Connection connection = new Connection(8080);
        private void Form1_Load(object sender, EventArgs e)
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus == OperationalStatus.Up)
                {
                    IPInterfaceProperties properties = ni.GetIPProperties();
                    foreach (UnicastIPAddressInformation ip in properties.UnicastAddresses)
                    {
                        textBox_Hostname.AppendText("ws://" + ip.Address.ToString() + ":8080\r\n");
                    }
                }
            }

            //Optional WSS
            //connection.LoadCertificate("cert.pfx", "");


            connection.OnConnectedClient += Connection_OnConnectedClient;
            connection.OnDisconnectedClient += Connection_OnDisconnectedClient;
            connection.OnError += Connection_OnError;
            connection.OnReceived += Connection_OnReceived;
        }


        private void Connection_OnError(object sender, EventHandlers.ErrorEventArgs e)
        {
            //Optional
        }

        private void Connection_OnDisconnectedClient(object sender, EventHandlers.DisconnectedClientEventArgs e)
        {
            //Invoke for Safety UI
            label_CountClients.Invoke(new Action(() =>
            {
                label_CountClients.Text = "Connected Clients : " + e.Count.ToString();
            }));
        }

        private void Connection_OnConnectedClient(object sender, EventHandlers.ConnectedClientEventArgs e)
        {
            //Invoke for Safety UI
            label_CountClients.Invoke(new Action(() =>
            {
                label_CountClients.Text = "Connected Clients : " + e.Count.ToString();
            }));
        }

        private void Connection_OnReceived(object sender, EventHandlers.ReceivedEventArgs e)
        {
            //Invoke for Safety UI
            textBox_ReceivedMessage.Invoke(new Action(() =>
            {
                textBox_ReceivedMessage.AppendText(e.Message + "\r\n");
            }));
        }


        private async void button_Start_Click(object sender, EventArgs e)
        {
            label_Status.Text = "Running";
            await connection.StartAsync();
        }

        private async void button_Stop_Click(object sender, EventArgs e)
        {
            label_Status.Text = "Stop";
            await connection.StopAsync();
        }

        private async void button_SendAll_Click(object sender, EventArgs e)
        {
            await connection.SendMessageAsync(textBox_SendAll.Text);
        }

        private async void button_SendRoom_Click(object sender, EventArgs e)
        {
            await connection.SendMessageToRoomAsync(textBox_Room.Text, textBox_SendRoom.Text);
        }
    }
}
