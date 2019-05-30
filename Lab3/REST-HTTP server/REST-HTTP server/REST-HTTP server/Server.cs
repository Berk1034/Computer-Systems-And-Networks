using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace REST_HTTP_server
{
    public enum Method
    {
        POST, GET, PUT, DELETE, COPY, MOVE
    };

    public class Server
    {
        private TcpListener MyTCPListener;
        public bool Working { get; private set; }

        public readonly string SERVER_NAME = "Server: HitovServ";
        public readonly string MIME_HEADER = "text/xml";
        private readonly string HTTP_VERSION = "HTTP/1.1";

        private Thread HandlingClientsThread;
        private Dictionary<string, TcpClient> clients;
        private string CurrentDirPath;
        public int bufferSize = 5000;

        public Server(int port)
        {
            MyTCPListener = new TcpListener(IPAddress.Any, port);
            clients = new Dictionary<string, TcpClient>();
            CurrentDirPath = Directory.GetCurrentDirectory() + @"\FILESERV\";
        }

        private int Post(string fileName, string content)// add content to the end of file
        {
            if (File.Exists(fileName))
            {
                try
                {
                    using (FileStream stream = new FileStream(fileName, FileMode.Open))
                    {
                        if (stream.CanSeek)
                        {
                            stream.Seek(0, SeekOrigin.End);
                            byte[] buffer = Encoding.UTF8.GetBytes(content);
                            stream.Write(buffer, 0, buffer.Length);
                        }
                        else
                            return 408;
                    }
                }
                catch (Exception exception)
                {
                    return 500;
                }
                return 200;
            }
            return 404;
        }

        private int Get(string fileName, out string result) // get content of the file
        {
            result = "";
            if (File.Exists(fileName))
            {
                using (FileStream stream = new FileStream(fileName, FileMode.Open))
                {
                    FileInfo fileInfo = new FileInfo(fileName);
                    byte[] buffer = new byte[fileInfo.Length];
                    try
                    {
                        stream.Read(buffer, 0, buffer.Length);
                    }
                    catch (Exception exception)
                    {
                        return 503;
                    }
                    result = Encoding.UTF8.GetString(buffer);
                }
                return 200;
            }
            return 404;
        }

        private int Delete(string fileName) // delete file
        {
            if (File.Exists(fileName))
            {
                try
                {
                    File.Delete(fileName);
                }
                catch (Exception exception)
                {
                    return 500;
                }
                return 200;
            }
            return 404;
        }

        private int Put(string fileName, string newContent) // rewrite file
        {
            if (File.Exists(fileName))
            {
                try
                {
                    using (FileStream stream = new FileStream(fileName, FileMode.Create))
                    {
                        byte[] buffer = Encoding.UTF8.GetBytes(newContent);
                        stream.Write(buffer, 0, buffer.Length);
                    }
                }
                catch (Exception exception)
                {
                    return 500;
                }
                return 200;
            }
            return 404;
        }

        private int Copy(string fileName, string copiedFileName) // copy the file
        {
            if (File.Exists(fileName))
            {
                try
                {
                    File.Copy(fileName, copiedFileName);
                }
                catch (Exception exception)
                {
                    return 500;
                }
                return 200;
            }
            return 404;
        }

        private int Move(string filePath, string dirPath) // move the file
        {
            if (File.Exists(filePath))
            {
                try
                {
                    File.Move(filePath, dirPath + @"\" + Path.GetFileName(filePath));
                }
                catch (Exception exception)
                {
                    return 505;
                }
                return 200;
            }
            return 404;
        }

        public void Start()
        {
            try
            {
                MyTCPListener.Start();
                Working = true;
            }
            catch (Exception exception)
            {
                return;
            }
            HandlingClientsThread = new Thread(() => { HandlingClients(); });
            HandlingClientsThread.Start();
        }

        public void Stop()
        {
            HandlingClientsThread.Abort();
        }

        public static string GetMethodName(string requestString)
        {
            string methodName = "";
            for (int i = 0; i < requestString.Length && requestString[i] != ' '; i++)
                methodName += requestString[i];
            return methodName;
        }

        private void HandlingClients()
        {
            while (Working)
            {
                TcpClient newClient = MyTCPListener.AcceptTcpClient();
                Thread clientThread = new Thread(() => { HandlingClient(newClient); });
                clientThread.Start();
            }
        }

        private void HandlingClient(TcpClient client)
        {
            byte[] buffer = new byte[bufferSize];
            NetworkStream netStream;
            netStream = client.GetStream();
            int bytesRead = netStream.Read(buffer, 0, buffer.Length);
            string requestString = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            requestString = Uri.UnescapeDataString(requestString);
            string method = Server.GetMethodName(requestString);
            int methodResult = 0;
            string result = "";
            Dictionary<string, string> requestParams = Server.GetValue(requestString);
            try
            {
                switch (method)
                {
                    case "GET":
                        methodResult = Get(CurrentDirPath + requestParams["filename"], out result);
                        SendResponse(netStream, methodResult, result);
                        break;
                    case "POST":
                        methodResult = Post(CurrentDirPath + requestParams["filename"], requestParams["content"]);
                        SendResponse(netStream, methodResult);
                        break;
                    case "PUT":
                        methodResult = Put(CurrentDirPath + requestParams["filename"], requestParams["content"]);
                        SendResponse(netStream, methodResult);
                        break;
                    case "DELETE":
                        methodResult = Delete(CurrentDirPath + requestParams["filename"]);
                        SendResponse(netStream, methodResult);
                        break;
                    case "COPY":
                        methodResult = Copy(CurrentDirPath + requestParams["filename"], CurrentDirPath + requestParams["to"]);
                        SendResponse(netStream, methodResult);
                        break;
                    case "MOVE":
                        methodResult = Move(CurrentDirPath + requestParams["filename"], CurrentDirPath + requestParams["to"]);
                        SendResponse(netStream, methodResult);
                        break;
                    default:
                        methodResult = 400;
                        SendResponse(netStream, methodResult);
                        break;
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message);
            }
            client.Close();
            netStream.Dispose();
            Thread.CurrentThread.Abort();
        }

        private void SendResponse(NetworkStream clientStream, int requestResult, string content = "")
        {
            byte[] buffer = Encoding.UTF8.GetBytes(content);
            if (SendHeader(clientStream, requestResult, buffer.Length))
            {
                try
                {
                    clientStream.Write(buffer, 0, buffer.Length);
                }
                catch (Exception exception)
                {
                    Console.WriteLine("{0}", exception.Message);
                }
            }
        }

        private bool SendHeader(NetworkStream requestStream, int requestResult, int contentLength)
        {
            string headerString = HTTP_VERSION + ' ' + requestResult.ToString() + ' ' + "\r\n";
            headerString += SERVER_NAME + "\r\n";
            headerString += "Content-Type: " + MIME_HEADER + "\r\n";
            headerString += "Accept-Ranges: bytes\r\n";
            headerString += "Content-Length: " + contentLength + "\r\n\r\n";
            byte[] buffer = Encoding.UTF8.GetBytes(headerString);
            try
            {
                requestStream.Write(buffer, 0, buffer.Length);
            }
            catch (Exception exception)
            {
                return false;
            }
            return true;
        }

        private static Dictionary<string, string> GetValue(string requestString)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();

            Regex valueRegex = new Regex(@"(?<=\')([\d\w\.\s\\]+)(?=\')");
            MatchCollection values = valueRegex.Matches(requestString);

            Regex idRegex = new Regex(@"([A-Za-z\d]+)(?=\=)");
            MatchCollection ids = idRegex.Matches(requestString);

            for (int i = 0; i < values.Count; i++)
            {
                result.Add(ids[i].Value, values[i].Value);
            }
            return result;
        }
    }
}
