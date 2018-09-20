﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sakuno.KanColle.Amatsukaze.Game.Proxy;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Sakuno.KanColle.Amatsukaze.Game.Parsers
{
    public static class ApiParserManager
    {
        public static Regex TokenRegex { get; } = new Regex(@"(?<=api_token=)\w+");

        static SortedList<string, ApiParserBase> r_Parsers = new SortedList<string, ApiParserBase>(StringComparer.OrdinalIgnoreCase);

        static ApiParserManager()
        {
            var rAssembly = Assembly.GetExecutingAssembly();

            var rParserTypes = rAssembly.GetTypes().Where(r => !r.IsAbstract && r.IsSubclassOf(typeof(ApiParserBase)));
            foreach (var rType in rParserTypes)
            {
                var rAttributes = rType.GetCustomAttributes<ApiAttribute>();
                foreach (var rAttribute in rAttributes)
                {
                    var rParser = (ApiParserBase)Activator.CreateInstance(rType);
                    rParser.Api = rAttribute.Name;

                    r_Parsers.Add(rAttribute.Name, rParser);
                }
            }
        }

        internal static ApiParserBase GetParser(string rpApi)
        {
            ApiParserBase rParser;
            if (!r_Parsers.TryGetValue(rpApi, out rParser))
                r_Parsers.Add(rpApi, rParser = new DefaultApiParser());

            return rParser;
        }

        public static void Process(ApiSession session)
        {
            var api = session.DisplayUrl;

            try
            {
                if (session.ResponseBody.Length <= 4 || BitConverter.ToInt64(session.ResponseBody, 0) != 0x7B3D617461647673)
                    return;

                if (!r_Parsers.TryGetValue(api, out var parser))
                    return;

                var stream = new MemoryStream(session.ResponseBody) { Position = 7 };
                var json = JObject.Load(new JsonTextReader(new StreamReader(stream)));

                var resultCode = (int)json["api_result"];
                if (resultCode != 1)
                {
                    Logger.Write(LoggingLevel.Error, string.Format(StringResources.Instance.Main.Log_Exception_API_Failed, api, resultCode));
                    return;
                }

                var info = new ApiInfo(session, api, session.Parameters, json);

                parser.Process(info);
            }
            catch (AggregateException e) when (e.InnerExceptions.Count == 1)
            {
                Logger.Write(LoggingLevel.Error, string.Format(StringResources.Instance.Main.Log_Exception_API_ParseException, e.InnerExceptions[0].Message));

                session.ErrorMessage = e.ToString();
                HandleException(session, e);
            }
            catch (Exception e)
            {
                Logger.Write(LoggingLevel.Error, string.Format(StringResources.Instance.Main.Log_Exception_API_ParseException, e.Message));

                session.ErrorMessage = e.ToString();
                HandleException(session, e);
            }
        }

        internal static void HandleException(ApiSession rpSession, Exception rException)
        {
            try
            {
                using (var rStreamWriter = new StreamWriter(Logger.GetNewExceptionLogFilename(), false, new UTF8Encoding(true)))
                {
                    rStreamWriter.WriteLine("Version:");
                    rStreamWriter.WriteLine(ProductInfo.Version);
                    rStreamWriter.WriteLine();
                    rStreamWriter.WriteLine(TokenRegex.Replace(rpSession.FullUrl, "***************************"));
                    rStreamWriter.WriteLine("Request Data:");
                    rStreamWriter.WriteLine(TokenRegex.Replace(rpSession.RequestBodyString, "***************************"));
                    rStreamWriter.WriteLine();
                    rStreamWriter.WriteLine("Exception:");
                    rStreamWriter.WriteLine(rException.ToString());
                    rStreamWriter.WriteLine();
                    rStreamWriter.WriteLine("Response Data:");
                    rStreamWriter.WriteLine(Regex.Unescape(rpSession.ResponseBodyString));
                }
            }
            catch { }
        }
    }
}
