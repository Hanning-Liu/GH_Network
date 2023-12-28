using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Network_TCP
{
    enum ConnectStatus
    {
        NoConnection,
        Connected,
        Disconnected
    }
    public class TCP_Server : GH_Component
    {
        private TcpListener tcpListener = null;
        private static bool listenerStarted = false;
        private TcpClient tcpClient = null;
        private Thread receiveThread;
        private Thread sendThread;
        private static readonly object receiveLock = new object();
        private static string receivedMessage = "";
        private static string textToSend = " ";
        private static string previousTextToSend = " ";
        private static ConnectStatus connectStatus = ConnectStatus.NoConnection;
        private static string connectedClientInfo = null; 
        private static bool newclientThreadsCreated = false;
        public TCP_Server()
          : base("TCP_Server", "Server",
              "The TCP server",
              "Network", "TCP")
        {
        }
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Listener IP Address", "IP", "The IP address to connect to.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Listener Port", "Port", "Port to establish TCP connection(default=12345).", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Start to listen", "Start", "True to start listening, false to stop listening.", GH_ParamAccess.item);
            pManager.AddTextParameter("Message to Send", "Message", "Message to send to the server", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Server status", "Status", "The status of the server.", GH_ParamAccess.list);
            pManager.AddTextParameter("Received Message", "Received", "Received message from the server", GH_ParamAccess.item);
            pManager.AddTextParameter("Connected clients", "clients", "List of connected clients.", GH_ParamAccess.list);
        }
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string status;
            string ip = "127.0.0.1";
            int port = 12345;
            bool listenOrNot = false;
            if (!DA.GetData(0, ref ip) || !DA.GetData(1, ref port) || !DA.GetData(2, ref listenOrNot) || !DA.GetData(3, ref textToSend)) return;
            if (listenOrNot == true)
            {
                // Create the server if there isn't.
                if (tcpListener == null && listenerStarted == false)
                {
                    try
                    {
                        IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
                        tcpListener = new TcpListener(ipEndPoint);
                        tcpListener.Start();
                        listenerStarted = true;
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, $"Server is listening on port {port}.");
                    }
                    catch (Exception ex)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Error: {ex.Message}");
                    }
                }
                // Handle the clients and receive data.
                else
                {
                    if (listenerStarted == true)
                    {
                        // See if there is a client connecting.
                        if (tcpListener.Pending())
                        {
                            tcpClient = tcpListener.AcceptTcpClient();
                            connectStatus = ConnectStatus.Connected;
                            connectedClientInfo = $"New client connected from {((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address}:{((IPEndPoint)tcpClient.Client.RemoteEndPoint).Port}";
                        }
                        // Create thread of the connected client if not yet.
                        if (tcpClient != null && connectStatus == ConnectStatus.Connected && newclientThreadsCreated == false)
                        {
                            // Create the ReceiveMessages thread.
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
                            // Create the SendMessages thread.
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
                            // Let the threads only be created one time.
                            newclientThreadsCreated = true;
                        }
                    }
                }
                status = $"Server is listening on port {port}.";
                if (connectedClientInfo != null)
                {
                    status = status + "\n" + connectedClientInfo;
                }
                if (connectStatus == ConnectStatus.Disconnected)
                {
                    connectedClientInfo = null;
                    string[] statuses = status.Split('\n');
                    status = statuses[0] + "\n(" + ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address + ":" + ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Port + ") Client disconnected.";
                }
                DA.SetData(0, status);
                DA.SetData(1, receivedMessage);
                DA.SetData(2, connectedClientInfo);
            }
            else
            {
                // Close the TcpClient when done
                if (tcpListener != null)
                {
                    if (receiveThread != null) receiveThread.Abort();
                    if (sendThread != null) sendThread.Abort();
                    newclientThreadsCreated = false;
                    if (tcpClient != null)
                    {
                        tcpClient.Close();
                        tcpClient = null;
                    }
                    tcpListener.Stop();
                    tcpListener = null;
                    listenerStarted = false;
                }
                connectStatus = ConnectStatus.NoConnection;
                connectedClientInfo = null;
                status = "Server isn't working!";
                DA.SetData(0, status);
            }
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return Resource.TCP_S;
            }
        }
        public override Guid ComponentGuid
        {
            get { return new Guid("FD4E7C28-F2EF-4AB6-8E09-650EBEF44067"); }
        }
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

                while (SocketExtensions.IsConnected(tcpClient.Client))
                {
                    try
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
                    catch (Exception ex)
                    {
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Error receiving messages: {ex.Message}");
                    }
                }
                connectStatus = ConnectStatus.Disconnected;
                newclientThreadsCreated = false;
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
                        previousTextToSend = textToSend;
                    }
                    else { }
                }
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Error sending messages: {ex.Message}");
            }
        }
        static class SocketExtensions
        {
            public static bool IsConnected(Socket socket)
            {
                try
                {
                    return !(socket.Poll(1, SelectMode.SelectRead) && socket.Available == 0);
                }
                catch (SocketException) { return false; }
            }
        }
    }
}