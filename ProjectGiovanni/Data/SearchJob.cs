
namespace ProjectGiovanni.Data
{
    public class SearchJob
    {
        public int SearchIteration { get; set; }

        public Location StepLocation { get; set; }

        public int StepNumber { get; set; }

        public object LockObj { get; set; }
    }
}
