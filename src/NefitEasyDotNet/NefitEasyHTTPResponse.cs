using System;

namespace NefitEasyDotNet
{
    class NefitEasyHttpResponse
    {
        public int Code { get; }
        public string[] HeaderData { get; }
        public string Payload { get; }
        
        public NefitEasyHttpResponse(string result)
        {
            var res = result.Split('\n');
            if (res.Length > 0)
            {
                if (res[0].StartsWith("HTTP"))
                {
                    var resCode = res[0].Split(' ');
                    Code = Convert.ToInt32(resCode[1]);
                }
            }
            HeaderData = new string[res.Length-4];
            for (var i = 1; i < res.Length - 3; i++)
            {
                HeaderData[i - 1] = res[i];
            }
            Payload = res[res.Length - 1];
        }
    }
}