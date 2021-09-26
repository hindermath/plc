#nullable enable

namespace KNR
{
    public partial class Parser
    {
        public Term ParseTerm()
        {
            Term t = new();
            TermNode ft = new();
            ft.Factor = ParseFactor();
            ft.IsDivision = false;
            t.TermNodes.Add(ft);
            
            //Console.WriteLine("Just parsed first factor and looking at " + current.Text + " and " + next.Text );
            while ((current.Text == "*") || (current.Text == "/"))
            {
                TermNode tn = new();
                if (current.Text == "/")
                {
                    ExpectAndConsume("/");
                    tn.IsDivision = true;
                }
                else
                {
                    ExpectAndConsume("*");
                    tn.IsDivision = false;
                }
                tn.Factor = ParseFactor();
                //Console.WriteLine("Just parsed another factor and looking at " + current.Text + " and " + next.Text );
                t.TermNodes.Add(tn);
            }

            return t;
        }
    }
}