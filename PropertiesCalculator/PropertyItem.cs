namespace PropertiesCalculator
{
    public class PropertyItem
    {
        public PropertyItem()
        {
        }

        public PropertyItem(string name, int startInd, int stopInd)
        {
            Name = name;
            StartInd = startInd;
            StopInd = stopInd;
        }

        public PropertyItem(string name, string units, int startInd, int stopInd)
        {
            Name = name;
            Units = units;
            StartInd = startInd;
            StopInd = stopInd;
        }

        public string Name { get; }
        public string Units { get; } = "*";
        public int StartInd { get; }
        public int StopInd { get; }

        public override string ToString()
        {
            return string.Format("{0} {1} {2} {3}", Name, Units, StartInd, StopInd);
        }
    }
}