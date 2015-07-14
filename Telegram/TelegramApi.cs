﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;

namespace Telegram
{
    public class TelegramApi
    {
        private const string BotUrl = @"https://api.telegram.org/bot{0}/{1}";

        private readonly string _token;
        private readonly HttpClient _httpClient = new HttpClient();
        private int _retryCount;
        private Task _pollingTask;

        public int RetryCount
        {
            get { return _retryCount; }
            set { _retryCount = value < 1 ? 1 : value; }
        }

        public int Offset { get; private set; }

        public ConcurrentDictionary<User, ConcurrentQueue<Message>> Updates { get; set; }

        public TelegramApi(string token, int offset = 0)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            _token = token;
            RetryCount = 1;
            Offset = offset;
            Updates = new ConcurrentDictionary<User, ConcurrentQueue<Message>>();
        }

        public User GetMe()
        {
            return ExecuteMethod<User>("GetMe");
        }

        protected IEnumerable<Update> GetUpdates()
        {
            IEnumerable<Update> updates = ExecuteMethod<List<Update>>("getUpdates",
                new Dictionary<string, object>()
                {
                    {"offset", Offset}
                });

            Offset = updates.Any() ? updates.Last().UpdateId + 1 : Offset;
            return updates;
        }

        public void StartPolling()
        {
            if (_pollingTask != null)
            {
                return;
            }

            _pollingTask = Task.Run(() =>
            {
                while (true)
                {
                    var updates = GetUpdates();
                    foreach (var update in updates)
                    {
                        if (!Updates.ContainsKey(update.Message.From))
                        {
                            Updates.GetOrAdd(update.Message.From, new ConcurrentQueue<Message>());
                        }
                        Updates[update.Message.From].Enqueue(update.Message);
                    }
                    Thread.Sleep(200);
                }
            });
        }

        public  Message SendMessage(int chatId, string text)
        { 
            return ExecuteMethod<Message>("sendMessage",
                new Dictionary<string, object>()
                {
                    {"chat_id", chatId},
                    {"text", text}
                });
        }

        private T ExecuteMethod<T>(string method, Dictionary<string, object> parameters = null) where T : new()
        {
            if (string.IsNullOrEmpty(method))
            {
                throw new ArgumentNullException(nameof(method));
            }

            string url = string.Format(BotUrl, _token, method);
            if (parameters != null)
            {
                var parametersArray = parameters
                    .Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value.ToString())}&").ToArray();

                url += $"?{string.Join("&", parametersArray)}";
            }
            
            string response = string.Empty;
            for (int i = 0; i <= RetryCount; i++)
            {
                try
                {
                    var r = _httpClient.GetStringAsync(url);
                    r.Wait();
                    response = Uri.UnescapeDataString(r.Result);
                    break;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    Thread.Sleep(1000);

                    if (i == RetryCount)
                    {
                        throw;
                    }
                }
            }

            if (string.IsNullOrEmpty(response))
            {
                return new T();
            }

            Response<T> t = JsonConvert.DeserializeObject<Response<T>>(response);

            if (t.Result == null)
            {
                throw new Exception($"Method {method} returned 'Ok' = {t.Ok} and 'Result' = null");
            }

            if (!t.Ok)
            {
                throw new Exception($"Method {method} returned 'Ok' = false");
            }
            return t.Result;
        }
    }
}
