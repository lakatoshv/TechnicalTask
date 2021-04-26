using System;
using System.Collections.Generic;

namespace TechnickalTask.Responses
{
    public class Response
    {
        public List<UrlResponse> UrlResponses { get; set; } = new();
        public string Error { get; set; }
    }
}