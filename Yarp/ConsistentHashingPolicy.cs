using Yarp.ReverseProxy.LoadBalancing;
using Yarp.ReverseProxy.Model;

namespace Yarp
{
    public sealed class ConsistentHashingPolicy : ILoadBalancingPolicy
    {
        private const string IdQueryName = "Id";

        public string Name => "ConsistentHashing";

        public DestinationState? PickDestination(HttpContext context, ClusterState cluster, IReadOnlyList<DestinationState> availableDestinations)
        {
            if (context.Request.Query.TryGetValue(IdQueryName, out var value) && value.Count != 0)
            {
                int id = int.Parse(value.First());

                if (id % 2 == 0)
                {
                    return availableDestinations.Where(destination => destination.DestinationId == "destination1").FirstOrDefault();
                }

                return availableDestinations.Where(destination => destination.DestinationId == "destination2").FirstOrDefault();
            }

            return FallbackRoundRobin(availableDestinations);
        }


        private string lastRoutedDestination = string.Empty;

        private DestinationState FallbackRoundRobin(IReadOnlyList<DestinationState> availableDestinations)
        {
            if (this.lastRoutedDestination == string.Empty)
            {
                this.lastRoutedDestination = "destination1";
            }    
            else if (this.lastRoutedDestination == "destination1")
            {
                this.lastRoutedDestination = "destination2";
            }
            else
            {
                // it must be "destination2"
                this.lastRoutedDestination = "destination1";
            }

            return availableDestinations
                .Where(dest => dest.DestinationId == this.lastRoutedDestination)
                .First();
        }
    }
}
