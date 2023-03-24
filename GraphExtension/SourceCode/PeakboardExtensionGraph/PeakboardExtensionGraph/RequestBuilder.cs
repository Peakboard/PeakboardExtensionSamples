﻿using System;
using System.Net.Http;
using System.Net.Http.Headers;

namespace PeakboardExtensionGraph
{
    public class RequestBuilder
    {
        private string _accessToken;
        private readonly string _baseUrl;

        public RequestBuilder(string accessToken, string url)
        {
            _accessToken = accessToken;
            _baseUrl = url;
        }

        public HttpRequestMessage GetRequest(out string requestUrl,
            string suffix = null, RequestParameters parameters = null)
        {
            // append url suffix e.g. https://graph.microsoft.com/v1.0/me + /messages
            string url = _baseUrl + suffix;

            string queryParams = "";

            // append query params into url
            if(parameters != null)
            {
                queryParams = "?";
                
                if (suffix == "/calendarview")
                {
                    // add required parameter for calendar view request
                    var start = DateTime.Now.ToString("yyyy-MM-ddThh:mm:ssZ");
                    var end = DateTime.Now.AddDays(7).ToString("yyyy-MM-ddThh:mm:ssZ");
                    queryParams += $"startdatetime={start}&enddatetime={end}";
                }
                
                // append filter
                if (!string.IsNullOrEmpty(parameters.Filter))
                {
                    queryParams += $"$filter={parameters.Filter}";
                }
                
                // append sorting order
                if (!string.IsNullOrEmpty(parameters.OrderBy))
                {
                    if (queryParams != "?")
                    {
                        queryParams += "&";
                    }

                    queryParams += $"$orderby={parameters.OrderBy}";
                }
                
                // append skipped entries
                if (parameters.Skip != 0)
                {
                    if (queryParams != "?")
                    {
                        queryParams += "&";
                    }

                    queryParams += $"$skip={parameters.Skip}";
                }

                // append number of requested entries
                if (parameters.Top > 0)
                {
                    if (queryParams != "?")
                    {
                        queryParams += "&";
                    }

                    queryParams += $"$top={parameters.Top}";
                }

                // append selected fields
                if (!string.IsNullOrEmpty(parameters.Select))
                {
                    if (queryParams != "?")
                    {
                        queryParams += "&";
                    }

                    queryParams += $"$select={parameters.Select}";
                }
            }

            queryParams = queryParams == "?" ? "" : queryParams;
            
            // build valid http request
            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(url+queryParams),
                Method = HttpMethod.Get
            };
        
            // append authorization header
            request.Headers.Authorization = new AuthenticationHeaderValue("bearer", _accessToken);
            
            // append consistency level header
            if (parameters != null && parameters.ConsistencyLevelEventual)
            {
                request.Headers.Add("ConsistencyLevel", "eventual");
            }

            requestUrl = url + queryParams;
            return request;
        }

        public void RefreshToken(string token)
        {
            _accessToken = token;
        }

    }
}