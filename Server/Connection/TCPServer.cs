﻿using PacketModel.Connection.EventArguments;
using PacketModel.Translator;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Server.Connection
{
    internal class TCPServer
    {
        #region Delegates

        public delegate void PacketReceivedEventHandler(object sender, PacketReceivedEventArgs e);

        public delegate void ClientConnectionChangedEventHandler(object sender, ClientConnectionChangedEventArgs e);

        #endregion Delegates

        #region Events

        public event PacketReceivedEventHandler PacketReceived;

        public event ClientConnectionChangedEventHandler ClientConnectionChanged;

        #endregion Events

        #region Event Raiser

        private void OnPacketReceived(PacketReceivedEventArgs e)
        {
            PacketReceived?.Invoke(this, e);
        }

        private void OnClientConnectionChanged(ClientConnectionChangedEventArgs e)
        {
            ClientConnectionChanged?.Invoke(this, e);
        }

        #endregion Event Raiser

        #region Fields

        private TcpListener tcpListener;
        private List<Client> clients;

        #endregion Fields

        #region Properties

        // notwendig, um clients in TcpClients mit foreach
        // durchlaufen zu koennen
        public IEnumerable<TcpClient> TcpClients
        {
            get
            {
                foreach (Client client in clients)
                {
                    yield return client.Tcpclient;
                }
            }
        }

        public int Port { get; private set; }

        public int NumberOfConnectedClients
        {
            get { return clients.Count; }
        }

        #endregion Properties

        /// <summary>
        /// Konstruktor der IP & Port einzeln akzeptiert.
        /// </summary>
        /// <param name="localaddr"></param>
        /// <param name="port"></param>
        public TCPServer(IPAddress localaddr, int port)
            : this()
        {
            tcpListener = new TcpListener(localaddr, port);
            Port = port;
        }

        /// <summary>
        /// Konstruktor der IPEndPoint (Kombination aus IP & Port) akzeptiert.
        /// </summary>
        /// <param name="localEP"></param>
        public TCPServer(IPEndPoint localEP)
            : this()
        {
            tcpListener = new TcpListener(localEP);
        }

        /// <summary>
        /// Standard-Konstruktor, nicht von außerhalb aufrufbar.
        /// </summary>
        protected TCPServer()
        {
            clients = new List<Client>();
        }

        /// <summary>
        /// Startet den Server.
        /// </summary>
        public void Start()
        {
            try
            {
                // Startet den Server.
                tcpListener.Start();
                // Beginnt mit dem Akzeptieren von Clienten (Asynchron, neuer Thread).
                // Ruft dann "acceptTcpClientCallback" auf.
                tcpListener.BeginAcceptTcpClient(acceptTcpClientCallback, null);
            }
            catch (Exception ex)
            {
                throw new Exception("Der Server konnte nicht gestartet werden.", ex);
            }
        }

        /// <summary>
        /// Stoppt den Server
        /// </summary>
        public void Stop()
        {
            try
            {
                // Stoppt den Server.
                tcpListener.Stop();
                // Sperrt die clients während der folgenden Aktion.
                // Dadurch kann nicht von anderen Threads auf clients zugegriffen werden.
                lock (clients)
                {
                    // Schließt die Verbindung zu allen Clienten.
                    foreach (Client client in clients)
                    {
                        client.Tcpclient.Client.Disconnect(false);
                    }
                    // Leert die clients-Liste.
                    clients.Clear();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Fehler beim Stoppen des Servers.", ex);
            }
        }

        /// <summary>
        /// Sendet ein object an alle Clienten
        /// </summary>
        /// <param name="bytes"></param>
        public void SendPacket(object data)
        {
            // Sendet das Paket an jeden Clienten.
            foreach (Client c in clients)
            {
                SendPacket(c.Tcpclient, PacketSerializer.Serialize(data));
            }
        }

        /// <summary>
        /// Sendet ein Object
        /// </summary>
        /// <param name="tcpClient"></param>
        /// <param name="bytes"></param>
        public void SendPacket(TcpClient client, object data)
        {
            byte[] bytes = PacketSerializer.Serialize(data);
            SendPacket(client, bytes);
        }

        /// <summary>
        /// Sendet ein Paket an einen Clienten.
        /// </summary>
        /// <param name="tcpClient"></param>
        /// <param name="bytes"></param>
        private void SendPacket(TcpClient tcpClient, byte[] encrypteddata)
        {
            try
            {
                if (tcpClient != null && tcpClient.Connected)
                {
                    // Holt den Stream und beginnt einen asynchronen Schreibvorgang (neuer Thread).
                    // Es wird "sendCallback" mit dem Client, an den gesendet werden soll,
                    // als Parameter "result" uebergeben.
                    NetworkStream networkStream = tcpClient.GetStream();
                    networkStream.BeginWrite(encrypteddata, 0, encrypteddata.Length, sendCallback, tcpClient);
                }
                else
                {
                    // Wenn der Client nicht mehr verbunden war,
                    // wird er aus der clients-Liste entfernt.
                    Client client = clients.Find(c => c.Tcpclient == tcpClient);
                    lock (clients)
                    {
                        clients.Remove(client);
                    }
                    OnClientConnectionChanged(new ClientConnectionChangedEventArgs(client.Tcpclient));
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Fehler beim Senden des Paketes.", ex);
            }
        }

        #region Callbacks

        /// <summary>
        /// Callback-Methode für das Akzeptieren von Clienten.
        /// Wird aufgerufen wenn ein Client sich verbindet.
        /// </summary>
        /// <param name="result"></param>
        private void acceptTcpClientCallback(IAsyncResult result)
        {
            try
            {
                // Prüfen ob der Server korrekt läuft und nicht beendet wurde (Microsoft-Fehler).
                if (tcpListener.Server != null && tcpListener.Server.IsBound)
                {
                    // Verbindungsversuch vom Clienten abschließen
                    TcpClient tcpClient = tcpListener.EndAcceptTcpClient(result);
                    // Puffergrößte für den Clienten setzen.
                    byte[] buffer = new byte[tcpClient.ReceiveBufferSize];
                    Client client = new Client(tcpClient, buffer);
                    // clients sperren und Clienten hinzufügen.
                    lock (this.clients)
                    {
                        this.clients.Add(client);
                    }
                    // Stream von Clienten holen und Lesevorgang für den Clienten starten.
                    // "readCallback" wird mit dem client in "result" aufgerufen.
                    NetworkStream networkStream = client.Networkstream;
                    networkStream.BeginRead(client.Buffer, 0, client.Buffer.Length, readCallback, client);
                    // ClientConnected Event ausführen.
                    OnClientConnectionChanged(new ClientConnectionChangedEventArgs(tcpClient));
                    // Akzeptieren neuer Clienten starten.
                    tcpListener.BeginAcceptTcpClient(acceptTcpClientCallback, null);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Fehler beim Verbindungsversuch eines Clienten.", ex);
            }
        }

        /// <summary>
        /// Callback-Methode zum Senden von Nachrichten.
        /// </summary>
        /// <param name="result"></param>
        private void sendCallback(IAsyncResult result)
        {
            try
            {
                // Client an den gesendet wurde wird als result übergeben.
                TcpClient tcpClient = result.AsyncState as TcpClient;
                // Schreibvorgang abschließen
                NetworkStream networkStream = tcpClient.GetStream();
                networkStream.EndWrite(result);
            }
            catch (Exception ex)
            {
                throw new Exception("Fehler beim Senden des Paketes.", ex);
            }
        }

        /// <summary>
        /// Callback-Methode zum Empfangen von Nachrichten.
        /// </summary>
        /// <param name="result"></param>
        private void readCallback(IAsyncResult result)
        {
            // Client von dem Nachricht erhalten wurde wird als result übergeben.
            Client client = result.AsyncState as Client;

            try
            {
                // Prüfung ob Client valide und verbunden ist.
                if (client == null) return;
                if (client.Tcpclient.Connected)
                {
                    // Stream holen und Lesevorgang abschließen.
                    NetworkStream networkStream = client.Networkstream;
                    int read = networkStream.EndRead(result);
                    // Falls 0 bytes gesendet wurden, wird der Client entfernt.
                    if (read == 0)
                    {
                        lock (this.clients)
                        {
                            this.clients.Remove(client);
                            // ClientDisconnected Event ausführen.
                            OnClientConnectionChanged(new ClientConnectionChangedEventArgs(((IPEndPoint)client.Tcpclient.Client.RemoteEndPoint).Address, false));
                            client.Tcpclient.Dispose();
                            return;
                        }
                    }

                    // Tatsächlich empfangene Bytes in neues Array kopieren.
                    byte[] actualBytes = new byte[read];
                    Array.Copy(client.Buffer, actualBytes, read);

                    // PacketReceived Event ausführen.
                    OnPacketReceived(new PacketReceivedEventArgs(client.Tcpclient, actualBytes, read));
                    // Neuen Lesevorgang mit Clienten starten.
                    client.ClearBuffer();
                    networkStream.BeginRead(client.Buffer, 0, client.Buffer.Length, readCallback, client);
                }
            }
            catch (System.IO.IOException)
            {
                // Bei IO-Fehlern den Client entfernen.
                if (client != null)
                {
                    lock (this.clients)
                    {
                        this.clients.Remove(client);
                    }
                    OnClientConnectionChanged(new ClientConnectionChangedEventArgs(client.Tcpclient));
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Fehler beim Empfangen eines Paketes.", ex);
            }
        }

        #endregion Callbacks
    }
}