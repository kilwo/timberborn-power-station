using Bindito.Core;
using RopePower.Rendering;
using RopePower.Ropes;
using RopePower.Stations;
using RopePower.UI;
using Timberborn.EntityPanelSystem;
using Timberborn.TemplateInstantiation;

namespace RopePower
{
    [Context("Game")]
    public class RopePowerConfigurator : Configurator
    {
        private class EntityPanelModuleProvider : IProvider<EntityPanelModule>
        {
            private readonly StationRopesFragment _stationRopesFragment;

            public EntityPanelModuleProvider(StationRopesFragment stationRopesFragment)
            {
                _stationRopesFragment = stationRopesFragment;
            }

            public EntityPanelModule Get()
            {
                EntityPanelModule.Builder builder = new EntityPanelModule.Builder();
                builder.AddMiddleFragment(_stationRopesFragment);
                return builder.Build();
            }
        }

        protected override void Configure()
        {
            Bind<PowerTransferStation>().AsTransient();
            Bind<PowerTransferStationRegistry>().AsSingleton();
            Bind<RopeConnectionService>().AsSingleton();
            Bind<RopePowerConnector>().AsSingleton();
            Bind<RopeBlockService>().AsSingleton();
            Bind<RopeBlock>().AsTransient();
            Bind<RopeRenderer>().AsSingleton();
            Bind<RopePreviewTooltip>().AsSingleton();
            Bind<RopePreviewRenderer>().AsSingleton();
            Bind<RopeConnectionAddingTool>().AsSingleton();
            Bind<RopeConnectionButtonFactory>().AsSingleton();
            Bind<StationRopesFragment>().AsSingleton();
            MultiBind<EntityPanelModule>().ToProvider<EntityPanelModuleProvider>().AsSingleton();
            MultiBind<TemplateModule>().ToProvider(ProvideTemplateModule).AsSingleton();
        }

        private static TemplateModule ProvideTemplateModule()
        {
            TemplateModule.Builder builder = new TemplateModule.Builder();
            builder.AddDecorator<PowerTransferStationSpec, PowerTransferStation>();
            builder.AddDecorator<RopeBlockSpec, RopeBlock>();
            return builder.Build();
        }
    }
}
