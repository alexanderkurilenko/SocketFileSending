using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace SocketLocalChat
{
    public partial class Form1 : Form
    {
        private void IP_Enter(object sender, EventArgs e)
        {
            if (IP.Text == (String)IP.Tag)
            {
                IP.Text = "";
            }
        }

        private void IP_Leave(object sender, EventArgs e)
        {
            if (String.IsNullOrWhiteSpace(IP.Text))
            {
                IP.Text = (String)IP.Tag;
            }
        }


        public Form1()
        {
            InitializeComponent();
            //Создаем поток для приема сообщений
            new Thread(new ThreadStart(Receiver)).Start();
            new Thread(new ThreadStart(FileReceiver)).Start();
        }

        //Метод потока
        protected void Receiver()
        {
            //Создаем Listener на порт "по умолчанию"
            TcpListener Listen = new TcpListener(7000);
            //Начинаем прослушку
            Listen.Start();
            //и заведем заранее сокет
            Socket ReceiveSocket;
            while (true)
            {
                try
                {
                    //Пришло сообщение
                    ReceiveSocket = Listen.AcceptSocket();
                    Byte[] Receive = new Byte[256];
                    //Читать сообщение будем в поток
                    using (MemoryStream MessageR = new MemoryStream())
                    {
                        //Количество считанных байт
                        Int32 ReceivedBytes;
                        do
                        {//Собственно читаем
                            ReceivedBytes = ReceiveSocket.Receive(Receive, Receive.Length, 0);
                            //и записываем в поток
                            MessageR.Write(Receive, 0, ReceivedBytes);
                            //Читаем до тех пор, пока в очереди не останется данных
                        } while (ReceiveSocket.Available > 0);
                        //Добавляем изменения в ChatBox
                        ChatBox.BeginInvoke(AcceptDelegate, new object[] { "Received " + Encoding.Default.GetString(MessageR.ToArray()), ChatBox });
                    }
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
                
            }
        }

        //Метод потока
        protected void FileReceiver()
        {          
            TcpListener Listen = new TcpListener(6999);
            Listen.Start();
            //и заведем заранее сокет
            Socket ReceiveSocket=Listen.AcceptSocket(); 
            NetworkStream stream=new NetworkStream(ReceiveSocket);
            byte[] buffer = new byte[8192];
            try
            {
                //stream = client.GetStream();
                int bytesRead = stream.Read(buffer, 0, 12);
                if (bytesRead == 0) return;

                ushort id = BitConverter.ToUInt16(buffer, 0);
                long len = BitConverter.ToInt64(buffer, 2);
                ushort nameLen = BitConverter.ToUInt16(buffer, 10);
                stream.Read(buffer, 0, nameLen);
                string fileName = Encoding.UTF8.GetString(buffer, 0, nameLen);

                if (id == 1)
                {
                    using (BinaryWriter writer = new BinaryWriter(new FileStream(fileName, FileMode.Create)))
                    {
                        int recieved = 0;
                        while (recieved < len)
                        {
                            bytesRead = stream.Read(buffer, 0, 8192);
                            recieved += bytesRead;
                            writer.Write(buffer, 0, bytesRead);
                            
                        }
                    }
                   
                }
                
            }
            catch (Exception)
            {
                stream.Close();
             
            }
            finally
            {
                stream.Flush();
            }

        }

        /// <summary>
        /// Отправляет сообщение в потоке на IP, заданный в контроле IP
        /// </summary>
        /// <param name="Message">Передаваемое сообщение</param>
        void ThreadSend(object Message)
        {
              try
            {
                  //Проверяем входной объект на соответствие строке
                String MessageText = "";
                if (Message is String)
                {
                    MessageText = Message as String;
                }
                else 
                    throw new Exception("На вход необходимо подавать строку");
               
                  Byte[] SendBytes = Encoding.Default.GetBytes(MessageText);
                //Создаем сокет, коннектимся
                IPEndPoint EndPoint = new IPEndPoint(IPAddress.Parse(IP.Text), 7000);
                Socket Connector = new Socket(EndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                Connector.Connect(EndPoint);
                Connector.Send(SendBytes);
                Connector.Close();
                //Изменяем поле сообщений (уведомляем, что отправили сообщение)
               
                 ChatBox.BeginInvoke(AcceptDelegate, new object[] { "Send " + MessageText, ChatBox });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
                
           
              
                
              
            
        }
      
        //Делегат доступа к контролам формы
        delegate void SendMsg(String Text, RichTextBox Rtb);
        
        SendMsg AcceptDelegate = (String Text, RichTextBox Rtb) =>
            {
                Rtb.Text += Text + "\n";     
            };

        //Обработчик кнопки
        private void Send_Click(object sender, EventArgs e)
        {
            
            new Thread(new ParameterizedThreadStart(ThreadSend)).Start(Message.Text);          
        }

        private void button1_Click(object sender, EventArgs e)
        {//Отправляем файл
            //Добавим на форму OpenFileDialog и вызовем его
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                //Коннектимся
                IPEndPoint EndPoint = new IPEndPoint(IPAddress.Parse(IP.Text), 6999);
                Socket Connector = new Socket(EndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                
                Connector.Connect(EndPoint);
                NetworkStream stream = new NetworkStream(Connector);
                //Получаем имя из полного пути к файлу
                
                string FileName =openFileDialog1.FileName;
                //Выделяем имя файла
                int index = FileName.Length - 1;
                while (FileName[index] != '\\' && FileName[index] != '/')
                {
                    index--;
                }
                //Получаем имя файла
                String resFileName = "";
                for (int i = index + 1; i < FileName.Length; i++)
                    resFileName += FileName[i];
                MessageBox.Show(openFileDialog1.FileName);
                FileInfo file = new FileInfo(@FileName);

                byte[] id = BitConverter.GetBytes((ushort)1);
                byte[] size = BitConverter.GetBytes(file.Length);
                byte[] fileName = Encoding.UTF8.GetBytes(file.Name);
                byte[] fileNameLength = BitConverter.GetBytes((ushort)fileName.Length);
                byte[] fileInfo = new byte[12 + fileName.Length];

                id.CopyTo(fileInfo, 0);
                size.CopyTo(fileInfo, 2);
                fileNameLength.CopyTo(fileInfo, 10);
                fileName.CopyTo(fileInfo, 12);

                stream.Write(fileInfo, 0, fileInfo.Length); //Размер файла, имя

                byte[] buffer = new byte[1024 * 32];
                int count;

                long sended = 0;

                using (FileStream fileStream = new FileStream(file.FullName, FileMode.Open))
                    while ((count = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        stream.Write(buffer, 0, count);
                        sended += count;
                        
                    }
                stream.Flush();            
                Connector.Close();
            }
        }
    }
}
