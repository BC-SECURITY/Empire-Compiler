namespace FixtureLib
{
    public static class Calculator
    {
        public static int Sum(params int[] values)
        {
            int total = 0;
            foreach (int value in values)
            {
                total += value;
            }
            return total;
        }
    }
}
