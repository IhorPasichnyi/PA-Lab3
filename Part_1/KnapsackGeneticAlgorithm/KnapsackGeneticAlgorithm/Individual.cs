namespace KnapsackGeneticAlgorithm
{
    public class Individual
    {
        public int[] Genes { get; set; } // 1 - предмет у рюкзаку, 0 - ні
        public int TotalValue { get; set; }
        public int TotalWeight { get; set; }

        public Individual(int geneCount)
        {
            Genes = new int[geneCount];
        }

        public Individual Clone()
        {
            Individual copy = new Individual(Genes.Length);
            Array.Copy(this.Genes, copy.Genes, Genes.Length);
            copy.TotalValue = this.TotalValue;
            copy.TotalWeight = this.TotalWeight;
            return copy;
        }
    }
}
