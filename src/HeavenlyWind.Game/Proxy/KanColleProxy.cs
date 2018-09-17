using Fiddler;
using Sakuno.KanColle.Amatsukaze.Extensibility;
using Sakuno.KanColle.Amatsukaze.Extensibility.Services;
using Sakuno.KanColle.Amatsukaze.Game.Parsers;
using Sakuno.KanColle.Amatsukaze.Game.Services;
using Sakuno.KanColle.Amatsukaze.Models;
using Sakuno.SystemInterop;
using Sakuno.SystemInterop.Net;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks.Dataflow;

namespace Sakuno.KanColle.Amatsukaze.Game.Proxy
{
    public static class KanColleProxy
    {
        public static IPropagatorBlock<NetworkSession, NetworkSession> SessionSource { get; }

        static Regex r_RemoveGoogleAnalyticsRegex = new Regex(@"gapush\(.+?\);", RegexOptions.Singleline);
        static Regex r_UserIDRegex { get; } = new Regex(@"(?:(?<=api_world%2Fget_id%2F)|(?<=api_world\\/get_id\\/)|(?<=api_auth_member\\/dmmlogin\\/))\d+");
        static Regex r_TokenResponseRegex { get; } = new Regex(@"(?<=\\""api_token\\"":\\"")\w+");

        static Regex r_SuppressReloadConfirmation = new Regex("(?<=if \\()confirm\\(\"エラーが発生したため、ページ更新します。\"\\)(?=\\) {)");

        static string[] r_BlockingList;

        static string r_UpstreamProxy;

        static ManualResetEventSlim r_TrafficBarrier;

        static KanColleProxy()
        {
            SessionSource = new BufferBlock<NetworkSession>();

            FiddlerApplication.BeforeRequest += FiddlerApplication_BeforeRequest;
            FiddlerApplication.OnReadResponseBuffer += FiddlerApplication_OnReadResponseBuffer;
            FiddlerApplication.ResponseHeadersAvailable += FiddlerApplication_ResponseHeadersAvailable;
            FiddlerApplication.BeforeResponse += FiddlerApplication_BeforeResponse;
            FiddlerApplication.BeforeReturningError += FiddlerApplication_BeforeReturningError;
            FiddlerApplication.AfterSessionComplete += FiddlerApplication_AfterSessionComplete;

            if (File.Exists(@"Data\BlockingList.lst"))
                r_BlockingList = File.ReadAllLines(@"Data\BlockingList.lst");
            else
                r_BlockingList = ArrayUtil.Empty<string>();

            Preference.Instance.Network.UpstreamProxy.Enabled.Subscribe(_ => UpdateUpstreamProxy());
            Preference.Instance.Network.UpstreamProxy.Host.Subscribe(_ => UpdateUpstreamProxy());
            Preference.Instance.Network.UpstreamProxy.Port.Subscribe(_ => UpdateUpstreamProxy());
            UpdateUpstreamProxy();
        }

        public static void Start()
        {
            var rStartupFlags = FiddlerCoreStartupFlags.ChainToUpstreamGateway;
            if (Preference.Instance.Network.AllowRequestsFromOtherDevices)
                rStartupFlags |= FiddlerCoreStartupFlags.AllowRemoteClients;

            var rPort = Preference.Instance.Network.Port.Default;
            if (Preference.Instance.Network.PortCustomization)
                rPort = Preference.Instance.Network.Port.Value;
            FiddlerApplication.Startup(rPort, rStartupFlags);
        }

        static void FiddlerApplication_BeforeRequest(Session rpSession)
        {
            if (r_BlockingList.Any(rpSession.uriContains))
            {
                rpSession.utilCreateResponseAndBypassServer();
                return;
            }

            if (!r_UpstreamProxy.IsNullOrEmpty())
                rpSession["x-OverrideGateway"] = r_UpstreamProxy;

            var rRequest = rpSession.oRequest;

            var rFullUrl = rpSession.fullUrl;
            var rPath = rpSession.PathAndQuery;

            NetworkSession rSession;
            ApiSession rApiSession = null;

            if (rPath.StartsWith("/kcsapi/"))
                rSession = rApiSession = new ApiSession(rFullUrl);
            else if (!rPath.StartsWith("/kcs2/index.php") && (rPath.StartsWith("/kcs2/") || rPath.StartsWith("/kcs/sound/") || rPath.StartsWith("/gadget_html5/")))
                rSession = new ResourceSession(rFullUrl, rPath);
            else
                rSession = new NetworkSession(rFullUrl);

            if (rApiSession != null && RequestFilterService.Instance.IsBlocked(rApiSession))
            {
                rSession.State = NetworkSessionState.Blocked;
                rpSession.utilCreateResponseAndBypassServer();
                return;
            }

            rSession.RequestBodyString = Uri.UnescapeDataString(rpSession.GetRequestBodyAsString());
            rSession.Method = rpSession.RequestMethod;

            rpSession.Tag = rSession;

            if (rFullUrl.OICEquals(GameConstants.GamePageUrl) || rFullUrl.OICEquals("http://www.dmm.com/netgame_s/kancolle/") || rFullUrl.OICEquals("http://games.dmm.com/detail/kancolle/") || rPath.OICEquals("/gadget/js/kcs_flash.js"))
                rpSession.bBufferResponse = true;

            var rResourceSession = rSession as ResourceSession;
            if (rResourceSession != null)
                CacheService.Instance.ProcessRequest(rResourceSession, rpSession);

            rSession.RequestHeaders = rpSession.RequestHeaders.Select(r => new SessionHeader(r.Name, r.Value)).ToArray();

            SessionSource.Post(rSession);

            if (!rpSession.bHasResponse && r_TrafficBarrier != null)
                r_TrafficBarrier.Wait();
        }

        static void FiddlerApplication_OnReadResponseBuffer(object sender, RawReadEventArgs e)
        {
            var rSession = e.sessionOwner.Tag as NetworkSession;
            if (rSession != null)
                rSession.LoadedBytes += e.iCountOfBytes;
        }
        static void FiddlerApplication_ResponseHeadersAvailable(Session rpSession)
        {
            var rSession = rpSession.Tag as NetworkSession;
            var rContentLength = rpSession.oResponse["Content-Length"];
            if (!rContentLength.IsNullOrEmpty() && rSession != null)
                rSession.ContentLength = int.Parse(rContentLength);
        }

        static void FiddlerApplication_BeforeResponse(Session rpSession)
        {
            var rSession = rpSession.Tag as NetworkSession;
            if (rSession != null)
            {
                if (rSession.State == NetworkSessionState.Requested)
                    rSession.State = NetworkSessionState.Responsed;

                var rResourceSession = rSession as ResourceSession;
                if (rResourceSession != null)
                    CacheService.Instance.ProcessResponse(rResourceSession, rpSession);

                if (rSession.FullUrl.OICEquals("http://www.dmm.com/netgame_s/kancolle/") || rSession.FullUrl.OICEquals("http://games.dmm.com/detail/kancolle/"))
                {
                    var rSource = rpSession.GetResponseBodyAsString();
                    rSource = r_RemoveGoogleAnalyticsRegex.Replace(rSource, string.Empty);

                    rpSession.utilSetResponseBody(rSource);
                }

                if (rSession.FullUrl.OICEquals(GameConstants.GamePageUrl))
                {
                    ForceOverrideStylesheet(rpSession);

                    var rSource = rpSession.GetResponseBodyAsString();
                    rSource = r_SuppressReloadConfirmation.Replace(rSource, "false");

                    rpSession.utilSetResponseBody(rSource);
                }

                if (rSession is ApiSession)
                    rpSession.utilDecodeResponse();

                rSession.StatusCode = rpSession.responseCode;
                rSession.ResponseHeaders = rpSession.ResponseHeaders.Select(r => new SessionHeader(r.Name, r.Value)).ToArray();
            }
        }

        static void FiddlerApplication_BeforeReturningError(Session rpSession)
        {
            var rSession = rpSession.Tag as NetworkSession;
            if (rSession != null)
            {
                rSession.State = NetworkSessionState.Error;
                rSession.ErrorMessage = rpSession.GetResponseBodyAsString();

                if (!Preference.Instance.Network.AutoRetry.Value)
                    return;

                var rApiSession = rSession as ApiSession;
                if (rApiSession != null && rpSession["X-RetryCount"].IsNullOrEmpty() && (rpSession.responseCode >= 500 && rpSession.responseCode < 600))
                    Retry(rApiSession, rpSession);
            }
        }

        static void FiddlerApplication_AfterSessionComplete(Session rpSession)
        {
            var rSession = rpSession.Tag as NetworkSession;
            if (rSession == null)
                return;

            rSession.StatusCode = rpSession.responseCode;

            switch (rSession)
            {
                case ApiSession api:
                    rSession.ResponseBody = rpSession.ResponseBody;
                    ApiParserManager.Process(api);
                    break;

                case ResourceSession resource:
                    CacheService.Instance.ProcessOnCompletion(resource, rpSession);
                    break;

                default:
                    rSession.ResponseBody = rpSession.ResponseBody;
                    break;
            }
        }

        static void UpdateUpstreamProxy()
        {
            var rUpstreamProxyPreference = Preference.Instance.Network.UpstreamProxy;
            if (rUpstreamProxyPreference.Enabled)
                r_UpstreamProxy = rUpstreamProxyPreference.Host.Value + ":" + rUpstreamProxyPreference.Port.Value;
            else
                r_UpstreamProxy = null;
        }

        static void ForceOverrideStylesheet(Session rpSession)
        {
            rpSession.utilDecodeResponse();
            rpSession.utilReplaceInResponse("</head>", @"<style type=""text/css"">
html { touch-action: none }

body {
    margin: 0;
    overflow: hidden;
}

#ntg-recommend, #dmm-ntgnavi-renew { display: none !important; }

#game_frame {
    position: fixed;
    left: 0;
    top: -16px;
    z-index: 255;
}
</style></head>");
        }

        static void InitializeTrafficBarrier()
        {
            try
            {
                r_TrafficBarrier = new ManualResetEventSlim(NetworkListManager.IsConnectedToInternet);
                NetworkListManager.ConnectivityChanged += delegate
                {
                    if (NetworkListManager.IsConnectedToInternet)
                        r_TrafficBarrier.Set();
                    else
                        r_TrafficBarrier.Reset();
                };
            }
            catch
            {
            }

            ServiceManager.Register<INetworkAvailabilityService>(new NetworkAvailabilityService());
        }

        static void Retry(ApiSession rpSession, Session rpFiddlerSession)
        {
            TaskDialog rDialog = null;

            var rMaxAutoRetryCount = Preference.Instance.Network.AutoRetryCount.Value;
            var rErrorMessage = rpSession.ErrorMessage;

            try
            {
                for (var i = 1; ; i++)
                {
                    if (i > rMaxAutoRetryCount)
                    {
                        if (!Preference.Instance.Network.AutoRetryConfirmation.Value)
                            return;

                        if (rDialog == null)
                            rDialog = new TaskDialog()
                            {
                                OwnerWindowHandle = ServiceManager.GetService<IMainWindowService>().Handle,

                                Instruction = StringResources.Instance.Main.MessageDialog_Proxy_AutoRetry,
                                Content = string.Format(StringResources.Instance.Main.MessageDialog_Proxy_AutoRetry_Message, rpSession.DisplayUrl),

                                Buttons =
                                {
                                    new TaskDialogCommandLink(TaskDialogCommonButton.Yes, StringResources.Instance.Main.MessageDialog_Proxy_AutoRetry_Button_Continue),
                                    new TaskDialogCommandLink(TaskDialogCommonButton.No, StringResources.Instance.Main.MessageDialog_Proxy_AutoRetry_Button_Abort),
                                },
                                ButtonStyle = TaskDialogButtonStyle.CommandLink,
                            };

                        if (i == rMaxAutoRetryCount + 2)
                            rDialog.Content = string.Format(StringResources.Instance.Main.MessageDialog_Proxy_AutoRetry_Message2, rpSession.DisplayUrl);

                        rDialog.Detail = rErrorMessage;

                        if (rDialog.Show().ClickedCommonButton == TaskDialogCommonButton.No)
                            return;
                    }

                    var rStringDictionary = new StringDictionary();
                    rStringDictionary.Add("X-RetryCount", i.ToString());

                    Logger.Write(LoggingLevel.Info, string.Format(StringResources.Instance.Main.Log_Proxy_AutoRetry, i.ToString(), rpSession.DisplayUrl));

                    var rNewSession = FiddlerApplication.oProxy.SendRequestAndWait(rpFiddlerSession.oRequest.headers, rpFiddlerSession.requestBodyBytes, rStringDictionary, null);
                    if (rNewSession.responseCode != 200)
                        rErrorMessage = rNewSession.GetResponseBodyAsString();
                    else
                    {
                        rpFiddlerSession.oResponse.headers = rNewSession.oResponse.headers;
                        rpFiddlerSession.responseBodyBytes = rNewSession.responseBodyBytes;
                        rpFiddlerSession.oResponse.headers["Connection"] = "close";

                        return;
                    }
                }
            }
            finally
            {
                rDialog?.Dispose();
            }
        }

        class NetworkAvailabilityService : INetworkAvailabilityService
        {
            public void EnsureNetwork() => r_TrafficBarrier?.Wait();
        }

        class RequestFilterService : IRequestFilterService
        {
            public static RequestFilterService Instance { get; } = new RequestFilterService();

            event Func<string, IDictionary<string, string>, bool> Filter;

            RequestFilterService()
            {
                ServiceManager.Register<IRequestFilterService>(this);
            }

            public void Register(Func<string, IDictionary<string, string>, bool> filter) => Filter += filter;

            public bool IsBlocked(ApiSession rpSession) => Filter == null ? false : Filter(rpSession.DisplayUrl, rpSession.Parameters);
        }
    }
}
