//    Copyright 2012 Ken Egozi
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Globalization;
using Microsoft.Phone.Controls;
using System.Windows.Navigation;
using System.Windows;

namespace AzuraMobileSdk
{

    public sealed partial class MobileServiceClient : IMobileServiceClient
    {
        internal static readonly JsonSerializer Serializer;

        private readonly string _applicationKey;
        private readonly IWebClientFactory _clientFactory;
        private readonly string _serviceUrl;

        public string CurrentAuthToken { get; private set; }
        public MobileServiceUser CurrentUser { get; private set; }
        private WebAuthenticationBrokerStruct _broker;
        private Action<MobileServiceUser, Exception> _successContinueWith;

        public MobileServiceClient(string serviceUrl, string applicationKey,IWebClientFactory clientFactory)
        {
            _serviceUrl = serviceUrl;
            _applicationKey = applicationKey;
            _clientFactory = clientFactory;
        }

        static MobileServiceClient()
        {
            Serializer = new JsonSerializer
                             {
                                 ContractResolver = new CamelCasePropertyNamesContractResolver(),
                                 DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                                 DateFormatHandling = DateFormatHandling.IsoDateFormat
                             };
        }

        public void LoginInBackground(string authenticationToken, Action<MobileServiceUser, Exception> continueWith)
        {
            // Proper Async Tasks Programming cannot integrate with Windows Phone (stupid) Async Mechanisim which use Events... (ex: UploadStringCompleted)
            //var asyncTask = new Task<MobileServiceUser>(() => this.StartLoginAsync(authenticationToken));
            //asyncTask.Start();
            //return asyncTask;

            if (authenticationToken == null)
            {
                throw new ArgumentNullException("authenticationToken");
            }

            if (string.IsNullOrEmpty(authenticationToken))
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Error! Empty Argument: {0}",
                        "authenticationToken"));
            }


            var client = _clientFactory.GetClient();
            var url = _serviceUrl + LoginAsyncUriFragment + "?mode=" + LoginAsyncAuthenticationTokenKey;

            client.Headers[HttpRequestHeader.ContentType] = RequestJsonContentType;
            var payload = new JObject();
            payload[LoginAsyncAuthenticationTokenKey] = authenticationToken;

            client.UploadStringCompleted += (x, args) =>
                                                {
                                                    if (args.Error != null)
                                                    {
                                                        var ex = args.Error;
                                                        if (args.Error.InnerException != null)
                                                            ex = args.Error.InnerException;
                                                        continueWith(null, ex);
                                                        return;
                                                    }
                                                    var result = JObject.Parse(args.Result);
                                                    CurrentAuthToken = result["authenticationToken"].Value<string>();
                                                    CurrentUser =
                                                        new MobileServiceUser(result["user"]["userId"].Value<string>());
                                                    continueWith(CurrentUser, null);
                                                };

            //Go!

            client.UploadStringAsync(new Uri(url), payload.ToString());
        }



        /// <summary>
        /// Log a user into a Mobile Services application given a provider name and optional token object.
        /// </summary>
        /// <param name="provider" type="MobileServiceAuthenticationProvider">
        /// Authentication provider to use.
        /// </param>
        /// <param name="token" type="JObject">
        /// Optional, provider specific object with existing OAuth token to log in with.
        /// </param>
        /// <returns>
        /// Task that will complete when the user has finished authentication.
        /// </returns>
        public void LoginWithBrowser(MobileServiceAuthenticationProvider provider, WebAuthenticationBrokerStruct broker,
                                     Action<MobileServiceUser, Exception> continueWith)
        {
            // Proper Async Tasks Programming cannot integrate with Windows Phone (stupid) Async Mechanisim which use Events... (ex: UploadStringCompleted)
            //var asyncTask =  new Task<MobileServiceUser>(() => this.StartLoginAsync(provider, authorizationBrowser));
            //asyncTask.Start();
            //return asyncTask;
            this._broker = broker;
            _successContinueWith += continueWith;

            if (this.LoginInProgress)
            {
                throw new InvalidOperationException("Error, Login is still in progress..");
            }
            if (!Enum.IsDefined(typeof (MobileServiceAuthenticationProvider), provider))
            {
                throw new ArgumentOutOfRangeException("provider");
            }

            var providerName = provider.ToString().ToLower();
            this.LoginInProgress = true;

            try
            {
                //Launch the OAuth flow.

                broker.Dispacher.BeginInvoke(() => { broker.LoadingGrid.Visibility = Visibility.Visible; });
                broker.AuthorizationBrowser.Navigating += this.OnAuthorizationBrowserNavigating;
                broker.AuthorizationBrowser.Navigated += this.OnAuthorizationBrowserNavigated;
                broker.AuthorizationBrowser.Navigate(
                    new Uri(this._serviceUrl + LoginAsyncUriFragment + "/" + providerName));
            }
            catch (Exception ex)
            {
                //on Error
                CompleteOAuthFlow(false, ex.Message);
            }
        }

        /// <summary>
        /// Handles the navigating event of the OAuth web browser control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void OnAuthorizationBrowserNavigated(object sender, NavigationEventArgs e)
        {
            _broker.AuthorizationBrowser.Navigated -= this.OnAuthorizationBrowserNavigated;
            _broker.Dispacher.BeginInvoke(() =>
                                              {
                                                  _broker.LoadingGrid.Visibility = Visibility.Collapsed;
                                                  _broker.AuthorizationBrowser.Visibility = Visibility.Visible;
                                              });
        }

        /// <summary>
        /// Handles the navigating event of the OAuth web browser control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void OnAuthorizationBrowserNavigating(object sender, NavigatingEventArgs e)
        {
            var uri = e.Uri;

            if (uri == null || !uri.AbsoluteUri.StartsWith(_serviceUrl + LoginAsyncDoneUriFragment)) 
                return;
            
            var fragments = ProcessFragments(uri.Fragment);

            string tokenString;
            var success = fragments.TryGetValue("token", out tokenString);
            var tokenJson = JObject.Parse(tokenString);

            if (success && tokenJson != null)
            {
                e.Cancel = true;

                //Save user info
                CurrentAuthToken = tokenJson[LoginAsyncAuthenticationTokenKey].Value<string>();
                CurrentUser = new MobileServiceUser(tokenJson["user"]["userId"].Value<string>());
                //Done with succses
                CompleteOAuthFlow(success);
            }
            else
            {
                CompleteOAuthFlow(false);
            }

            //TODO: check if MobileServices return Error ==> StartsWith(this.serviceUrl+ LoginAsyncUriFragment) && Contains("error");
        }

        /// <summary>
        /// Process the URI fragment string.
        /// </summary>
        /// <param name="fragment">The URI fragment.</param>
        /// <returns>The key-value pairs.</returns>
        private Dictionary<string, string> ProcessFragments(string fragment)
        {
            var processedFragments = new Dictionary<string, string>();

            if (fragment[0] == '#')
            {
                fragment = fragment.Substring(1);
            }

            var fragmentParams = fragment.Split('&');

            foreach (string fragmentParam in fragmentParams)
            {
                var keyValue = fragmentParam.Split('=');

                if (keyValue.Length == 2)
                {
                    processedFragments.Add(keyValue[0], HttpUtility.UrlDecode(keyValue[1]));
                }
            }

            return processedFragments;
        }

        public void Logout()
        {
            CurrentUser = null;
            CurrentAuthToken = null;
        }

        public void Get(string relativeUrl, Action<string, Exception> continuation)
        {
            Execute("GET", relativeUrl, string.Empty, continuation);
        }

        public void Post(string relativeUrl, object payload, Action<string, Exception> continuation)
        {
            Execute("POST", relativeUrl, payload, continuation);
        }

        public void Delete(string relativeUrl, Action<Exception> continuation)
        {
            Execute("DELETE", relativeUrl, string.Empty, (s, err) => continuation(err));
        }

        public void Patch(string relativeUrl, object payload, Action<string, Exception> continuation)
        {
            Execute("PATCH", relativeUrl, payload, continuation);
        }

        public async Task<string> Get(string relativeUrl)
        {
            var client = GetWebClient();
            var url = BuildUrl(relativeUrl);
            return await client.DownloadStringTaskAsync(url);
        }

        public Task<string> Post(string relativeUrl, object payload)
        {
            var client = GetWebClient();
            var url = BuildUrl(relativeUrl);
            return client.UploadStringTaskAsync(url, "POST", ObtainUploadData(payload));
        }

        public Task Delete(string relativeUrl)
        {
            var client = GetWebClient();
            var url = BuildUrl(relativeUrl);
            return client.UploadStringTaskAsync(url, "DELETE", string.Empty);
        }

        public Task<string> Patch(string relativeUrl, object payload)
        {
            var client = GetWebClient();
            var url = BuildUrl(relativeUrl);
            return client.UploadStringTaskAsync(url, "PATCH", ObtainUploadData(payload));
        }

        public MobileServiceTable GetTable(string tableName)
        {
            return new MobileServiceTable(this, tableName);
        }

        public MobileServiceTable<TItem> GetTable<TItem>(string tableName)
        {
            return new MobileServiceTable<TItem>(this, tableName);
        }

        public MobileServiceTable<TItem> GetTable<TItem>()
        {
            var tableName = typeof (TItem).Name;
            return GetTable<TItem>(tableName);
        }

        private Uri BuildUrl(string relativeUrl)
        {
            return new Uri(_serviceUrl + relativeUrl);
        }

        private WebClient GetWebClient()
        {
            var client = _clientFactory.GetClient();
            SetMobileServiceHeaders(client);
            return client;
        }

        private static string ObtainUploadData(object payload)
        {
            var payloadString = payload as string;
            if (payloadString == null && payload != null)
            {
                var buffer = new StringBuilder();
                using (var writer = new StringWriter(buffer))
                {
                    Serializer.Serialize(writer, payload);
                }
                payloadString = buffer.ToString();
            }

            return payloadString;
        }

        private void Execute(string method, string relativeUrl, object payload, Action<string, Exception> continuation)
        {
            var endpointUrl = _serviceUrl + relativeUrl;
            var client = _clientFactory.GetClient();
            client.UploadStringCompleted += (x, args) => OperationCompleted(args, continuation);
            client.DownloadStringCompleted += (x, args) => OperationCompleted(args, continuation);
            SetMobileServiceHeaders(client);
            if (method == "GET")
            {
                client.DownloadStringAsync(new Uri(endpointUrl));
                return;
            }

            var payloadString = payload as string;
            if (payloadString == null && payload != null)
            {
                var buffer = new StringBuilder();
                using (var writer = new StringWriter(buffer))
                {
                    Serializer.Serialize(writer, payload);
                }
                payloadString = buffer.ToString();
            }
            client.UploadStringAsync(new Uri(endpointUrl), method, payloadString);
        }

        private void OperationCompleted(AsyncCompletedEventArgs args, Action<string, Exception> continuation)
        {
            if (args.Error != null)
            {
                var ex = args.Error;
                var webException = ex as WebException;
                if (webException != null)
                {
                    var response = webException.Response as HttpWebResponse;
                    if (response != null)
                    {
                        var code = response.StatusCode;
                        var msg = response.StatusDescription;
                        try
                        {
                            using (var reader = new StreamReader(response.GetResponseStream()))
                            {
                                msg += "\r\n" + reader.ReadToEnd();
                            }
                        }
                        catch (Exception)
                        {
                            msg += "\r\nResponse body could not be extracted";
                        }
                        ex = new Exception(string.Format("Http error [{0}] - {1}", (int) code, msg), ex);
                    }
                }
                continuation(null, ex);
                return;
            }
            string result = null;
            var uploadStringCompletedEventArgs = args as UploadStringCompletedEventArgs;
            if (uploadStringCompletedEventArgs != null)
                result = uploadStringCompletedEventArgs.Result;
            var downloadStringCompletedEventArgs = args as DownloadStringCompletedEventArgs;
            if (downloadStringCompletedEventArgs != null)
                result = downloadStringCompletedEventArgs.Result;
            if (result == null)
            {
                throw new InvalidOperationException(
                    "args should be either UploadStringCompletedEventArgs or DownloadStringCompletedEventArgs");
            }
            continuation(result, null);
        }

        private void SetMobileServiceHeaders(WebClient client)
        {
            if (CurrentAuthToken != null)
            {
                client.Headers["X-ZUMO-AUTH"] = CurrentAuthToken;
            }

            if (_applicationKey != null)
            {
                client.Headers["X-ZUMO-APPLICATION"] = _applicationKey;
            }
        }

        /// <summary>
        /// Complete the OAuth flow.
        /// </summary>
        /// <param name="success">Whether the operation was successful.</param>
        private void CompleteOAuthFlow(bool success, string errorMsg = null)
        {
            this.LoginInProgress = false;
            _broker.AuthorizationBrowser.Navigated -= this.OnAuthorizationBrowserNavigated;
            _broker.AuthorizationBrowser.Navigating -= this.OnAuthorizationBrowserNavigating;

            //Hide Broker UI
            _broker.Dispacher.BeginInvoke(() =>
                                              {
                                                  _broker.AuthorizationBrowser.NavigateToString(String.Empty);
                                                  _broker.AuthorizationBrowser.Visibility = Visibility.Collapsed;
                                                  _broker.LoadingGrid.Visibility = Visibility.Collapsed;
                                              });

            // Invoke ContinueWith Method
            if (_successContinueWith != null)
            {
                _successContinueWith(this.CurrentUser, success ? null : new InvalidOperationException(errorMsg));
            }

        }
    }
}