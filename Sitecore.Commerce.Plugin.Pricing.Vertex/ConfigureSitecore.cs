using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Sitecore.Commerce.Core;
using Sitecore.Commerce.Plugin.Carts;
using Sitecore.Commerce.Plugin.Orders;
using Sitecore.Commerce.Plugin.Pricing.Vertex.Pipelines.CalculateCart.Blocks;
using Sitecore.Commerce.Plugin.Pricing.Vertex.Pipelines.ReleasedOrdersMinion.Blocks;
using Sitecore.Framework.Configuration;
using Sitecore.Framework.Pipelines.Definitions.Extensions;

namespace Sitecore.Commerce.Plugin.Pricing.Vertex
{
    /// <summary>
    /// The configure sitecore class.
    /// </summary>
    public class ConfigureSitecore : IConfigureSitecore
    {
        /// <summary>
        /// The configure services.
        /// </summary>
        /// <param name="services">
        /// The services.
        /// </param>
        public void ConfigureServices(IServiceCollection services)
        {
            var assembly = Assembly.GetExecutingAssembly();
            services.RegisterAllPipelineBlocks(assembly);

            services.Sitecore().Pipelines(config => config
                .ConfigurePipeline<ICalculateCartPipeline>(configure => configure
                        .Add<CalculateCartTaxBlock>().Before<CalculateCartTotalsBlock>(), "main", 10000
                )
                .ConfigurePipeline<IReleasedOrdersMinionPipeline>(configure => configure
                    .Add<CreateVertexInvoiceRequest>().Before<MoveReleasedOrderBlock>()
                )
            );

            services.RegisterAllCommands(assembly);
        }
    }
}