﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Web;
using cogbot;
using cogbot.Actions;
using HttpServer;
using OpenMetaverse;
using OpenMetaverse.Http;

namespace cogbot.Utilities
{
    //.. tonight i am writing them a webserver in .net 
    public class WriteLineToResponse
    {
        public IHttpResponse response;
        public WriteLineToResponse(IHttpResponse r)
        {
            response = r;
        }
        public void WriteLine(string str, object[] args)
        {
            string s = string.Format(str, args);
            if (response != null)
            {
                response.AddToBody(s);
            }
            else
            {
                Console.WriteLine("no respnse object for " + s);
            }
        }
    }

    public class ClientManagerHttpServer
    {
        HttpServer.HttpListener _listener;
        private int _port;
        private BotClient _botClient;
        public ClientManagerHttpServer(BotClient bc, int port)
        {
            _botClient = bc;
            _port = port;
            Init();
        }
        public void Init()
        {
            _listener = HttpServer.HttpListener.Create(log4netLogWriter.Instance, IPAddress.Any, _port);
            _listener.Accepted += _listener_Accepted;
            _listener.Set404Handler(_listener_404);
            workArroundReuse();
            try
            {
                _listener.Start(10);
                Console.WriteLine("Ready for HTTPD port " + _port);
                _botClient.WriteLine("Ready for HTTPD port " + _port);
            }
            catch (Exception e)
            {
                _botClient.WriteLine("NOT OK for HTTPD port " + _port + "\n" +e);
            }
        }

        private void workArroundReuse()
        {
            try
            {
                TcpClient client = new TcpClient();
                client.Connect("localhost", _port);
            } catch
            {
                
            }
        }

        private void _listener_404(IHttpClientContext context, IHttpRequest request, IHttpResponse response)
        {
            UUID capsID;
         
            bool success;

            string path = request.Uri.PathAndQuery;//.TrimEnd('/');
            string pathd =  HttpUtility.UrlDecode(request.Uri.PathAndQuery);//.TrimEnd('/');
            Console.WriteLine("_listener " + path + " from " + request.RemoteEndPoint);
            if (request.UriPath.EndsWith(".ico"))
            {
                response.Status = HttpStatusCode.NotFound;
                response.Send();                
            }
            bool useHtml = true;
            string botname = GetVariable(request, "bot", _botClient.GetName());
            string cmd = GetVariable(request, "cmd", "aiml");
            string username = GetVariable(request, "username", GetVariable(request, "ident", null));
            string text = GetVariable(request, "text", GetVariable(request, "entry", pathd.TrimStart('/')));           
            var wrresp = new WriteLineToResponse(response);
            if (path.StartsWith("/chat?"))
            {
                useHtml = false;
            }
            try
            {
                if (useHtml)
                {
                    AddToBody(response, "<html>");
                    AddToBody(response, "<head>");
                    AddToBody(response, "<title>" + botname + "</title>");
                    AddToBody(response, "</head>");
                    AddToBody(response, "<body>");
                    AddToBody(response, "<pre>");
                    AddToBody(response, "bot = " + botname);
                    AddToBody(response, "cmd = " + cmd);
                    AddToBody(response, "text = " + text);
                    AddToBody(response, "</pre>");
                    AddToBody(response, "<a href='" + request.Uri.PathAndQuery + "'>"
                                        + request.Uri.PathAndQuery + "</a>");
                }
                
                AddToBody(response, "<pre>");
                AddToBody(response, "\n<!-- Begin Response !-->");
                
                CmdResult res;
                if (String.IsNullOrEmpty(username))
                {
                    //res = _botClient.ExecuteCommand(cmd + " " + text, wrresp.WriteLine);
                    res = _botClient.ExecuteCommand(cmd + " @ UNKNOWN_PARTNER - " + text, wrresp.WriteLine);
                }
                else
                {
                    res = _botClient.ExecuteCommand(cmd + " @ " + username + " - " + text, wrresp.WriteLine);
                }
                AddToBody(response, "");
                AddToBody(response, "\n<!-- End Response !-->");
                AddToBody(response, "</pre>");
                if (useHtml)
                {
                    AddToBody(response, "</body>");
                    AddToBody(response, "</html>");
                }
            } finally
            {
                wrresp.response = null;
                response.Status = HttpStatusCode.OK;
                response.Send();                
            }
        }

        static public void AddToBody(IHttpResponse response, string text)
        {
            response.AddToBody(text + Environment.NewLine);
        }

        static public string GetVariable(IHttpRequest request, string varName, string defaultValue)
        {
            if (request.QueryString.Contains(varName))
            {
                return HttpUtility.UrlDecode(request.QueryString[varName].Value);
            }
            if (request.Param.Contains(varName))
            {
                var single = request.Param[varName].Value;
                if (!String.IsNullOrEmpty(single)) return HttpUtility.UrlDecode(single);
                var values = request.Param[varName].Values;
                if (values.Count > 0) return HttpUtility.UrlDecode(values[0]);
            }
            return HttpUtility.UrlDecode(defaultValue);
        }

        private void _listener_Accepted(object sender, ClientAcceptedEventArgs e)
        {
            Console.WriteLine("_listener_Accepted " + e.Socket);
        }
    }
}
