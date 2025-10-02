namespace HeartMonitor
{
    internal class Average(TimeSpan cutoff)
    {
        private readonly TimeSpan _cutoff = cutoff;
        private readonly List<(DateTime time, int value)> _values = new();
        public void Add(int value)
        {
            var now = DateTime.UtcNow;
            _values.Add((now, value));
            _values.RemoveAll(v => now - v.time > _cutoff);
        }
        public double GetAverage()
        {
            if (_values.Count == 0)
                return 0;
            return _values.Average(v => v.value);
        }
    }
}
