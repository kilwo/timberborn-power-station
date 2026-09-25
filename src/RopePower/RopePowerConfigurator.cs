using Bindito.Core;

namespace RopePower
{
    [Context("Game")]
    public class RopePowerConfigurator : Configurator
    {
        protected override void Configure()
        {
            Bind<RopePowerLoadMarker>().AsSingleton();
        }
    }
}
