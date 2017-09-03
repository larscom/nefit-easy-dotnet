namespace NefitEasyDotNet.Models.Internal
{
    class NefitJson<T>
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public int Recordable { get; set; }
        public int Writable { get; set; }
        public T Value { get; set; }
    }
}