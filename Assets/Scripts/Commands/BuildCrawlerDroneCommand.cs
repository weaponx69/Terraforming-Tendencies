using UnityEngine;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Player;

namespace GameDevTV.RTS.Commands
{
    [CreateAssetMenu(fileName = "Build Crawler Drone", menuName = "Buildings/Commands/Build Crawler Drone", order = 130)]
    public class BuildCrawlerDroneCommand : BaseCommand
    {
        [SerializeField] private FoundryCrawler.DroneType droneType;
        [SerializeField] private int pipeCost = 3;

        public FoundryCrawler.DroneType DroneType => droneType;
        public int PipeCost => pipeCost;

        public override bool RequiresClickToActivate => false;

        public override bool CanHandle(CommandContext context)
        {
            return context.Commandable is FoundryCrawler;
        }

        public override void Handle(CommandContext context)
        {
            if (context.Commandable is FoundryCrawler crawler)
            {
                crawler.TryBuildDrone(droneType);
            }
        }

        public override bool IsLocked(CommandContext context)
        {
            if (context.Commandable is FoundryCrawler crawler)
            {
                return crawler.PipeBuffer < pipeCost;
            }
            return true;
        }
    }
}