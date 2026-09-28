using Bindito.Core;
using CablePowerTransfer.Rendering;
using CablePowerTransfer.Cables;
using CablePowerTransfer.Stations;
using CablePowerTransfer.UI;
using Timberborn.EntityPanelSystem;
using Timberborn.TemplateInstantiation;

namespace CablePowerTransfer
{
    [Context("Game")]
    public class CablePowerTransferConfigurator : Configurator
    {
        private class EntityPanelModuleProvider : IProvider<EntityPanelModule>
        {
            private readonly StationCablesFragment _stationCablesFragment;

            public EntityPanelModuleProvider(StationCablesFragment stationCablesFragment)
            {
                _stationCablesFragment = stationCablesFragment;
            }

            public EntityPanelModule Get()
            {
                EntityPanelModule.Builder builder = new EntityPanelModule.Builder();
                builder.AddMiddleFragment(_stationCablesFragment);
                return builder.Build();
            }
        }

        protected override void Configure()
        {
            Bind<PowerTransferStation>().AsTransient();
            Bind<PowerTransferStationRegistry>().AsSingleton();
            Bind<CableConnectionService>().AsSingleton();
            Bind<CablePowerConnector>().AsSingleton();
            Bind<PowerCableBlockService>().AsSingleton();
            Bind<PowerCableBlock>().AsTransient();
            Bind<CableRenderer>().AsSingleton();
            Bind<StationAnimator>().AsTransient();
            Bind<CablePreviewTooltip>().AsSingleton();
            Bind<CablePreviewRenderer>().AsSingleton();
            Bind<CableConnectionAddingTool>().AsSingleton();
            Bind<CableConnectionButtonFactory>().AsSingleton();
            Bind<StationCablesFragment>().AsSingleton();
            MultiBind<EntityPanelModule>().ToProvider<EntityPanelModuleProvider>().AsSingleton();
            MultiBind<TemplateModule>().ToProvider(ProvideTemplateModule).AsSingleton();
        }

        private static TemplateModule ProvideTemplateModule()
        {
            TemplateModule.Builder builder = new TemplateModule.Builder();
            builder.AddDecorator<PowerTransferStationSpec, PowerTransferStation>();
            builder.AddDecorator<PowerCableBlockSpec, PowerCableBlock>();
            builder.AddDecorator<StationAnimatorSpec, StationAnimator>();
            return builder.Build();
        }
    }
}
