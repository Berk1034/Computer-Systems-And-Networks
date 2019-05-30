using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Collections.Generic;

namespace REST_HTTP_client
{
    partial class Program
    {
        public static string localHost = @"http://localhost:1034/";

        public class Request
        {
            public static string GetMethod(string requestString)
            {
                string methodString = "";
                for (int i = 0; i < requestString.Length && requestString[i] != '?'; i++)
                    methodString += requestString[i];

                return methodString;
            }
        }

        static void Main(string[] args)
        {
            while (true)
            {
                Console.Write("The REQUEST>>" + localHost);
                string requestString = Console.ReadLine();

                if (requestString != @"\exit")
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(localHost + requestString);
                    request.Method = Request.GetMethod(requestString);
                    try
                    {
                        var response = request.GetResponse();
                        using (var dataStream = response.GetResponseStream())
                        {
                            StreamReader reader = new StreamReader(dataStream);
                            string responseFromServer = reader.ReadToEnd();
                            if(responseFromServer == "")
                            {
                                responseFromServer = "Success";
                            }
                            Console.WriteLine("Server returned: " + responseFromServer);
                        }
                        response.Close();
                    }
                    catch (WebException exception)
                    {
                        Console.WriteLine(exception.Message);
                    }
                }
                else
                    break;
            }
        }
    }
}
