using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace TCPServer01
{
    public partial class Form1 : Form
    {
        
        TcpListener mTCPListener;
        TcpClient mTCPClient;
        byte[] mRx;

        private List<ClientNode> mlClientSocks;


        public Form1()
        {
            InitializeComponent();
            mlClientSocks = new List<ClientNode>(2);
            CheckForIllegalCrossThreadCalls = false;
        }

        IPAddress findMyIPV4Address()
        {
            string strThisHostName = string.Empty;
            IPHostEntry thisHostDNSEntry = null;
            IPAddress[] allIPsOfThisHost = null;
            IPAddress ipv4Ret = null;

            try
            {
                strThisHostName = System.Net.Dns.GetHostName();
                printLine(strThisHostName);
                thisHostDNSEntry = System.Net.Dns.GetHostEntry(strThisHostName);
                allIPsOfThisHost = thisHostDNSEntry.AddressList;

                for (int idx = allIPsOfThisHost.Length - 1; idx >= 0; idx--)
                {
                    if (allIPsOfThisHost[idx].AddressFamily == AddressFamily.InterNetwork)
                    {
                        ipv4Ret = allIPsOfThisHost[idx];
                        break;
                    }
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
            }

            return ipv4Ret;
        }

        private void btnStartListening_Click(object sender, EventArgs e)
        {
            IPAddress ipaddr;
            int nPort;

            if (!int.TryParse(tbPort.Text, out nPort))
            {
                nPort = 23000;
            }
            if (!IPAddress.TryParse(tbIPAddress.Text, out ipaddr))
            {
                MessageBox.Show("Invalid IP address supplied.");
                return;
            }
            
            mTCPListener = new TcpListener(IPAddress.Any, nPort);

            try
            {
                mTCPListener.Start();
                mTCPListener.BeginAcceptTcpClient(onCompleteAcceptTcpClient, mTCPListener);
            }
            catch(Exception exc)
            {
                MessageBox.Show(exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void onCompleteAcceptTcpClient(IAsyncResult iar)
        {
            TcpListener tcpl = (TcpListener)iar.AsyncState;
            TcpClient tclient = null;
            ClientNode cNode = null;

            try
            {
                tclient = tcpl.EndAcceptTcpClient(iar);

                printLine("Client Connected...");

                tcpl.BeginAcceptTcpClient(onCompleteAcceptTcpClient, tcpl);

                lock (mlClientSocks)
                {
                    mlClientSocks.Add((cNode = new ClientNode(tclient, new byte[200000], new byte[200000], tclient.Client.RemoteEndPoint.ToString())));
                    lbClients.Items.Add(cNode.ToString());                    
                }
                
                tclient.GetStream().BeginRead(cNode.Rx, 0, cNode.Rx.Length, onCompleteReadFromTCPClientStream, tclient);

                //mRx = new byte[5000];
                //mTCPClient.GetStream().BeginRead(mRx, 0, mRx.Length, onCompleteReadFromTCPClientStream, mTCPClient);


            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void onCompleteReadFromTCPClientStream(IAsyncResult iar)
        {
            TcpClient tcpc;
            int nCountReadBytes = 0;
            string strRecv;
            ClientNode cn = null;

            try
            {
                lock(mlClientSocks)
                {
                    tcpc = (TcpClient)iar.AsyncState;

                    cn = mlClientSocks.Find(x => x.strId == tcpc.Client.RemoteEndPoint.ToString());

                    nCountReadBytes = tcpc.GetStream().EndRead(iar);

                    if (nCountReadBytes == 0)// this happens when the client is disconnected
                    {
                        MessageBox.Show("Client disconnected.");
                        mlClientSocks.Remove(cn);
                        lbClients.Items.Remove(cn.ToString());
                        return;
                    }

                    strRecv = Encoding.ASCII.GetString(cn.Rx, 0, nCountReadBytes).Trim();
                    string path;
                    path = "C:\\Users\\karim\\Desktop\\" + strRecv;
                    printLine(path);
                    if (File.Exists(path) == true)
                    {
                        /*
                        //in case of images:
                        Image mImage = Image.FromFile(path);
                        SendData(ImageToByte(mImage), cn);
                        */
                        
                        //in case of html files:
                        SendData(FileToByte(path), cn);
                    }
                    else if (File.Exists(path) == false)
                    {
                        SendData(Encoding.ASCII.GetBytes("ERROR 404 File Not Found!"), cn);
                    }
                    //strRecv = Encoding.ASCII.GetString(mRx, 0, nCountReadBytes);
                    printLine(DateTime.Now + " - " + cn.ToString() + ": " + strRecv);
                    //pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;
                    //pictureBox1.Image = BytesToImg(cn.Rx);
                    //webBrowser1.DocumentText = strRecv;
                    cn.Rx = new byte[200000];

                    tcpc.GetStream().BeginRead(cn.Rx, 0, cn.Rx.Length, onCompleteReadFromTCPClientStream, tcpc);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                lock (mlClientSocks)
                {
                    printLine("Client disconnected: " + cn.ToString());
                    mlClientSocks.Remove(cn);
                    lbClients.Items.Remove(cn.ToString());
                }

            }
        }

        public void printLine(string _strPrint)
        {
            tbConsoleOutput.Invoke(new Action<string>(doInvoke), _strPrint);
        }

        public void doInvoke(string _strPrint)
        {
            tbConsoleOutput.Text = _strPrint + Environment.NewLine + tbConsoleOutput.Text;
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            Image mImage = Image.FromFile("C:\\Users\\karim\\Desktop\\Picture.jpg");
            
            SendData(ImageToByte(mImage));
        }

        private void onCompleteWriteToClientStream(IAsyncResult iar)
        {
            try
            {
                TcpClient tcpc = (TcpClient)iar.AsyncState;
                tcpc.GetStream().EndWrite(iar);
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnFindIPv4IP_Click(object sender, EventArgs e)
        {
            IPAddress ipa = null;
            ipa = findMyIPV4Address();

            if (ipa != null)
            {
                tbIPAddress.Text = ipa.ToString();
            }
        }

        private void btnFindIPv4IP_Click_1(object sender, EventArgs e)
        {
            IPAddress ipa = null;

            ipa = findMyIPV4Address();
            if (ipa != null)
            {
                tbIPAddress.Text = ipa.ToString();
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void tbIPAddress_TextChanged(object sender, EventArgs e)
        {

        }

        public byte[] ImageToByte(Image imageIn)
        {
            MemoryStream ms = new MemoryStream();
            imageIn.Save(ms, System.Drawing.Imaging.ImageFormat.Gif);
            return ms.ToArray();
        }
        /*
        public Image ByteToImage(byte[] byteArrayIn)
        {
            MemoryStream ms = new MemoryStream(byteArrayIn);
            Image returnImage = Image.FromStream(ms);
            return returnImage;
        }
        */
        private Image getScreen()
        {
            Size s = Screen.PrimaryScreen.Bounds.Size;
            Bitmap b = new Bitmap(s.Height, s.Width);
            Graphics g = Graphics.FromImage(b);
            g.CopyFromScreen(0, 0, 0, 0, s);

            return b;
        }
        /*
        private byte[] BmpToBytes(Image bmp)
        {
            MemoryStream ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Jpeg);
            byte[] bmpBytes = ms.GetBuffer();
            bmp.Dispose();
            ms.Close();

            return bmpBytes;
        }*/

        private Image BytesToImg(byte[] bmpBytes)
        {
            MemoryStream ms = new MemoryStream(bmpBytes);
            Image img = Image.FromStream(ms);
            return img;
        }

        private void SendData([Optional] byte[]RequestedBytes , [Optional]ClientNode RequestedClient)
        {
            if (lbClients.Items.Count <= 0) return;
            //if (string.IsNullOrEmpty(tbPayload.Text)) return;

            ClientNode cn = RequestedClient;

            lock (mlClientSocks)
            {
                try
                {
                    if (cn == null) cn = mlClientSocks.Find(x => x.strId == lbClients.SelectedItem.ToString());
                    else cn = RequestedClient;
                    cn.Tx = new byte[200000];
                }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }


                try
                {
                    if (cn != null)
                    {
                        if (cn.tclient != null)
                        {
                            if (cn.tclient.Client.Connected)
                            {
                                //Image mImage = Image.FromFile("C:\\Users\\karim\\Desktop\\Picture.jpg");
                                //cn.Tx = ImageToByte(mImage);
                                cn.Tx = RequestedBytes;
                                //cn.Tx = Encoding.ASCII.GetBytes(tbPayload.Text);
                                //cn.Tx = RequestedBytes;
                                cn.tclient.GetStream().BeginWrite(cn.Tx, 0, cn.Tx.Length, onCompleteWriteToClientStream, cn.tclient);
                            }
                        }
                    }
                }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        public byte[] FileToByte(string filepath)
        {
            byte[] returnBytes;
            returnBytes = File.ReadAllBytes(filepath);
            return returnBytes;
        }
    }
}
