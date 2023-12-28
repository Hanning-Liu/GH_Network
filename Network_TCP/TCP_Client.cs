using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Network_TCP
{
    public class TCP_Client : GH_Component
    {
        private TcpClient tcpClient;
        Thread receiveThread;
        Thread sendThread;
        private static readonly object receiveLock = new object();
        private static string receivedMessage = "";
        private static string textToSend = " ";
        private static string previousTextToSend = " ";

        public TCP_Client()
          : base("TCP_Client", "Client",
            "The TCP client",
            "Network", "TCP")
        {
        }
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("IP Address", "IP", "The IP address to connect to.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Port", "Port", "Port to establish TCP connection(default=12345).", GH_ParamAccess.item);
            pManager.AddTextParameter("Message to Send", "Message", "Message to send to the server", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Connect to Server", "Connect", "Connect or disconnect from the server", GH_ParamAccess.item);
        }
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Received Message", "Received", "Received message from the server", GH_ParamAccess.item);
        }
        protected override void SolveInstance(IGH_DataAccess DA)
        {

            string ip = "127.0.0.1";
            int port = 12345;
            bool toggle = false;
            if (!DA.GetData(0, ref ip) || !DA.GetData(1, ref port) || !DA.GetData(2, ref textToSend) || !DA.GetData(3, ref toggle)) return;
            if (tcpClient == null)
            {
                tcpClient = new TcpClient();
            }
            if (toggle)
            {
                if (tcpClient.Connected) { }
                else
                {
                    try
                    {
                        tcpClient.Connect(ip, port);
                    }
                    catch (Exception ex)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Error: {ex.Message}");
                    }
                    if (tcpClient.Connected)
                    {
                        try
                        {

                            receiveThread = new Thread(ReceiveMessages);
                            receiveThread.Name = "ReceiveMessagesThread";
                            receiveThread.Start(tcpClient);
                        }
                        catch (Exception ex)
                        {
                            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Error: {ex.Message}");
                        }

                        try
                        {
                            sendThread = new Thread(SendMessages);
                            sendThread.Name = "SendMessagesThread";
                            sendThread.Start(tcpClient);
                        }
                        catch (Exception ex)
                        {
                            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Error: {ex.Message}");
                        }
                    }
                }
                previousTextToSend = textToSend;
            }
            else
            {
                // Close the TcpClient when done
                if (tcpClient != null)
                {
                    if (receiveThread != null) receiveThread.Abort();
                    if (sendThread != null) sendThread.Abort();
                    tcpClient.Close();
                    tcpClient = null;
                }
            }
            DA.SetData(0, receivedMessage);
        }
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return Resource.TCP_C;
            }
        }
        public override Guid ComponentGuid => new Guid("09e35695-1e85-4fb9-a529-3cdbfc6b9000");
        void ReceiveMessages(object clientObj)
        {
            TcpClient tcpClient = (TcpClient)clientObj;

            try
            {
                // Create a network stream for reading data
                NetworkStream networkStream = tcpClient.GetStream();

                // Buffer to store incoming data
                byte[] buffer = new byte[1024];
                int bytesRead;

                while (true)
                {
                    // Read incoming data
                    bytesRead = networkStream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        // Convert the received bytes to a string
                        string newMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        lock (receiveLock)
                        {
                            receivedMessage = newMessage;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Error receiving messages: {ex.Message}");
            }
        }
        void SendMessages(object clientObj)
        {
            TcpClient tcpClient = (TcpClient)clientObj;
            try
            {
                // Create a network stream for writing data
                NetworkStream networkStream = tcpClient.GetStream();

                // Send messages from the console input
                while (true)
                {
                    if (previousTextToSend != textToSend)
                    {
                        string message = textToSend;

                        // Convert the message to bytes
                        byte[] data = Encoding.UTF8.GetBytes(message);

                        // Send the data to the server
                        networkStream.Write(data, 0, data.Length);
                    }
                    else { }

                }
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Error sending messages: {ex.Message}");
            }
        }
    }
}