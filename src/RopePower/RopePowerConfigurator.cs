using Bindito.Core;
using RopePower.Ropes;
using RopePower.Stations;
using Timberborn.Debugging;
using Timberborn.TemplateInstantiation;

namespace RopePower
{
    [Context("Game")]
    public class RopePowerConfigurator : Configurator
    {
        protected override void Configure()
        {
            Bind<PowerTransferStation>().AsTransient();
            Bind<PowerTransferStationRegistry>().AsSingleton();
            Bind<RopeConnectionService>().AsSingleton();
            Bind<RopePowerConnector>().AsSingleton();
            MultiBind<IDevModule>().To<RopeLinkDevModule>().AsSingleton();
            MultiBind<TemplateModule>().ToProvider(ProvideTemplateModule).AsSingleton();
        }

        private static TemplateModule ProvideTemplateModule()
        {
            TemplateModule.Builder builder = new TemplateModule.Builder();
            builder.AddDecorator<PowerTransferStationSpec, PowerTransferStation>();
            return builder.Build();
        }
    }
}
