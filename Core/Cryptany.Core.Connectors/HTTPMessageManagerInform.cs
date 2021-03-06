/*
   Copyright 2006-2017 Cryptany, Inc.

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/
using System;
using System.Collections.Specialized;
using System.IO;
using System.Messaging;
using System.Net;
using System.Text;
using System.Threading;
using System.Web;
using System.Xml;
using Cryptany.Core.Management.WMI;
using Cryptany.Common.Utils;
using Cryptany.Common.Logging;
using Cryptany.Core;
using Cryptany.Core.Management;
using Cryptany.Core.Interaction;

namespace Cryptany.Core
{
    /// <summary>
    /// Manages all aspects of HTTP message creation. Translates string representation of the 
    /// message attributes into adjacent classes.
    /// </summary>
    public class HTTPMessageManagerInform : HTTPMessageManager
    {

        // все настройки HTTP
        protected HTTPInformSettings _HTTPSettings;

        /// <summary>
        /// Constructor of the HTTPMM. Reads up the database and prepares caches for work.
        /// </summary>
        public HTTPMessageManagerInform(ConnectorSettings cs, ILogger logger) : base(cs, logger)
        {
            ReadyToSendMessages = new ManualResetEvent(true);
        }

        public override event EventHandler MessageReceived;
        public override event EventHandler MessageSent;
        public override event MessageStateChangedEventHandler MessageStateChanged;
        public override event StateChangedEventHandler StateChanged;

        /// <summary>
        /// Should be called for initialization of HTTPMM
        /// </summary>
        protected override void Init(AbstractConnectorSettings settings)
        {
            // Create connector for HTTP
            _HTTPSettings = settings as HTTPInformSettings;
            HTTPConn = new HTTPConnector(_HTTPSettings.Scheme, _HTTPSettings.Host, _HTTPSettings.Port, _HTTPSettings.Path,
                                         _HTTPSettings.RequestURL, _HTTPSettings.SystemId, _HTTPSettings.Password, this);
            try
            {
                // Initialize HTTP listening 
                if (HTTPConn.Start() == false)
                {
                    throw new Exception("HTTP connection initialization failed!");
                }
            }
            catch (Exception e)
            {
                try
                {
                    StateChanged(this, new StateChangedEventArgs(ConnectorState.Error, e.ToString()));
                }
                catch
                {
                    
                }
                if (Logger != null)
                {
                    Logger.Write(new LogMessage("Exception in HTTPMessageManagerInform Init method: " + e, LogSeverity.Error));
                }
            }
        }

        /// <summary>            
        /// Should be called for finalization of HTTPMM
        /// </summary>
        public override void Dispose()
        {
            base.Dispose();
            // Stop HTTP listening 
            if (HTTPConn != null)
            {
                HTTPConn.Stop(true);
            }
        }

        /// <summary>
        /// Creates and initializes HTTP GET request (Applied to Async mode only)
        /// </summary>
        /// <returns>Initialized byte[]</returns>
        protected virtual byte[] Create_GET_request(OutputMessage outputMessage)
        {
            // Create GET request
            string queryString = "";
            if (_HTTPSettings.IsAsyncMode) // асинхронный режим отправки 
            {
                queryString += "?User=" + HttpUtility.UrlEncode(HTTPConn.SystemID, _HTTPSettings.DataCoding);
                queryString += "&Password=" + HttpUtility.UrlEncode(HTTPConn.ServerPass, _HTTPSettings.DataCoding);
                queryString += "&Phone=" + HttpUtility.UrlEncode(outputMessage.Destination, _HTTPSettings.DataCoding);
                queryString += "&Type=SMS";
                queryString += "&Body=" +
                               HttpUtility.UrlEncode(((TextContent) outputMessage.Content).MsgText,
                                                     _HTTPSettings.DataCoding);
                queryString += "&Category=" +
                               HttpUtility.UrlEncode(outputMessage.HTTP_Category, _HTTPSettings.DataCoding);
                queryString += "&Sender=" + HttpUtility.UrlEncode(outputMessage.Source, _HTTPSettings.DataCoding);
                if (outputMessage.HTTP_Category.StartsWith("mf")) // оператор - МегаФон
                {
                    // совпадает с параметром Category
                    queryString += "&DeliverRoute=" +
                                   HttpUtility.UrlEncode(outputMessage.HTTP_Category, _HTTPSettings.DataCoding);
                }
            }
            byte[] bytes = _HTTPSettings.DataCoding.GetBytes(queryString);
            return bytes;
        }

        /// <summary>
        /// Creates and initializes HTTP POST request
        /// </summary>
        /// <returns>Initialized byte[]</returns>
        protected virtual byte[] Create_POST_request(OutputMessage outputMessage)
        {
            // Create POST request
            string postString = "";
            byte[] bytes = _HTTPSettings.DataCoding.GetBytes(postString);
            return bytes;
        }

        /// <summary>
        /// HTTP GET message receiving - (i.e. initial request): for Incore only
        /// 1. Parse incoming GET parameters
        /// 2. Create MSMQ message and send it to Router main input MSMQ queue 
        /// </summary>
        /// <returns>
        /// Inbox message ID
        /// </returns>
        public virtual Guid Receive(HTTPConnector conn, NameValueCollection queryString)
        {
            
            Guid inboxMsgID = Guid.Empty;
            if (queryString != null && queryString.Count > 0)
            {
                // 1. Parse incoming GET parameters
                HTTPMsgParams = DecodeRequest(queryString);
                if (HTTPMsgParams != null)
                {
                    // 2. Create MSMQ message and send it to Router main input MSMQ queue 
                    inboxMsgID = Send_MSMQ_MessageToRouterInputQueue(HTTPMsgParams);
                    try
                    {
                        MessageReceived(this, new EventArgs());
                    }
                    catch
                    {

                    }
                }
            }
            return inboxMsgID;
        }

        /// <summary>
        /// HTTP POST message receiving
        /// 1. Parse incoming POST request body
        /// 2. Save output message delivery status to DB
        /// 3. Create MSMQ message and send it to Router main input MSMQ queue / Create output message stub
        /// </summary>
        /// <returns>
        /// Inbox message ID
        /// </returns>
        public virtual Guid Receive(HTTPConnector conn, string requestBody)
        {
            Guid inboxMsgID = Guid.Empty;
            if (!string.IsNullOrEmpty(requestBody))
            {
                // 1. Parse incoming POST parameters
                HTTPMsgParams = DecodeRequest(requestBody);
                if (HTTPMsgParams.DeliveryStatus == "") // user request message
                {
                    // 2. Create MSMQ message and send it to Router main input MSMQ queue 
                    inboxMsgID = Send_MSMQ_MessageToRouterInputQueue(HTTPMsgParams);
                    try
                    {
                        MessageReceived(this, new EventArgs());
                    }
                    catch
                    {

                    }
                }
                else // delivery status message
                {
                    // 2. Save output message delivery status to DB
                    try
                    {
                        MessageStateChanged(this, new MessageStateChangedEventArgs(HTTPMsgParams.DeliveryStatus, "", HTTPMsgParams.MID.ToString()));
                    }
                    catch
                    {

                    }
                    
                    // 3. Create output message stub
                    OutputMessage outMsg = new OutputMessage();
                    outMsg.ID = IdGenerator.NewId;
                    outMsg.HTTP_UID = HTTPMsgParams.UID;
                    outMsg.HTTP_DeliveryStatus = HTTPMsgParams.DeliveryStatus;
                    outMsg.Content = new TextContent("");
                    outMsg.IsPayed = false;
                    HTTPConn.OutputMsg = outMsg;
                    HTTPConn.OutgoingMessageReady.Set();
                    inboxMsgID = outMsg.InboxMsgID;

                }
            }
            return inboxMsgID;
        }
    }


    public class HTTPInformSettings : AbstractConnectorSettings
    {
        public Encoding DataCoding;

        //public bool IsAsyncMode;

        public bool IsAsyncMode_Post;
        public bool LoggingEnabled;

        public string Path;

        public string RequestURL;
        public string Scheme;
        public string ContentType;
        public string Salt;

        public TimeSpan SyncMode_RespWaitTime;
        
        public HTTPInformSettings(int SMSCCode) : base(SMSCCode)
        {
        }

        protected override void InitSettings(XmlDocument settings)
        {
            IsAsyncMode = Convert.ToBoolean(settings["Common"].Attributes["IsAsyncMode"].Value);
            IsAsyncMode_Post = Convert.ToBoolean(settings["Common"].Attributes["IsAsyncMode_Post"].Value);
            Scheme = settings["Common"].Attributes["Scheme"].Value;
            
            //зачем лишнее поле?
            IPAddress = settings["Common"].Attributes["Host"].Value;
            Host = settings["Common"].Attributes["Host"].Value;
            
            Port = settings["Common"].Attributes["Port"].Value;
            Path = settings["Common"].Attributes["Path"].Value;
            RequestURL = settings["Common"].Attributes["RequestURL"].Value;
            SystemId = settings["Common"].Attributes["SystemId"].Value;
            Password = settings["Common"].Attributes["Password"].Value;
            Salt = settings["Common"].Attributes["Salt"].Value;
            LoggingEnabled = Convert.ToBoolean(settings["Common"].Attributes["LoggingEnabled"].Value);
            SyncMode_RespWaitTime = new TimeSpan(0, 0,
                                                 Convert.ToInt32(settings["Common"].Attributes["SyncMode_RespWaitTime"].Value));
            DataCoding = Encoding.GetEncoding(Convert.ToInt32(settings["Common"].Attributes["DataCoding"].Value));
        }
    }
}
