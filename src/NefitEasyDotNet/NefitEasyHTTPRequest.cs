using System.Xml;

namespace NefitEasyDotNet
{
    class NefitEasyHttpRequest
    {
        public string Url { get; }
        public string Payload { get; }
        public string Jid { get; }
        public string To { get; }

        public NefitEasyHttpRequest(string url, string jid, string to, string payload = null)
        {
            Url = url;
            Payload = payload;
            Jid = jid;
            To = to;
        }

        public override string ToString()
        {
            var xmlDoc = new XmlDocument();
            var root = xmlDoc.CreateElement(string.Empty, "message", string.Empty);
            root.SetAttribute("from", Jid);
            root.SetAttribute("to", To);
            var body = xmlDoc.CreateElement(string.Empty, "body", string.Empty);
            root.AppendChild(body);
            xmlDoc.AppendChild(root);
            var result = Url;
            result += " HTTP/1.1\n";
            if (!string.IsNullOrEmpty(Payload))
            {
                result = "PUT " + result;
                result += "Content-Type: application/json\n";
                result += $"Content-Length:{Payload.Length}\n";
            }
            else
            {
                result = "GET " + result;
            }
            result += "User-Agent: NefitEasy\n\n";
            result += Payload;
            body.InnerText = result;
            return xmlDoc.InnerXml.Replace("\n", "&#13;\n");
        }
    }
}