using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sitecore.Commerce.Core;
using Sitecore.Commerce.Core.Commands;
using Sitecore.Commerce.Plugin.Carts;
using Sitecore.Commerce.Plugin.Catalog;
using Sitecore.Commerce.Plugin.Fulfillment;
using Sitecore.Commerce.Plugin.Shops;

namespace Sitecore.Commerce.Plugin.Pricing.Vertex.Commands
{
    public class BaseVertexCommerceCommand : CommerceCommand
    {
        private readonly ILocalizableTermPipeline _localizableTermPipeline;
        private readonly IGetShopPipeline _getShopPipeline;
        private readonly IGetSellableItemPipeline _getSellableItemPipeline;

        public BaseVertexCommerceCommand(IServiceProvider serviceProvider, ILocalizableTermPipeline localizableTermPipeline, IGetShopPipeline getShopPipeline, IGetSellableItemPipeline getSellableItemPipeline) : base(serviceProvider)
        {
            _localizableTermPipeline = localizableTermPipeline;
            _getShopPipeline = getShopPipeline;
            _getSellableItemPipeline = getSellableItemPipeline;
        }

        protected async Task<string> GetProductSKU(CommerceContext context, CartProductComponent cartProductComponent)
        {
            if (String.IsNullOrEmpty(cartProductComponent.Catalog))
            {
                context.Logger.LogInformation($"{this.GetType().Name} - cartProductComponent.Catalog is null");
                cartProductComponent.Catalog = "";
            }

            var sellableItem = await _getSellableItemPipeline.Run(
                new ProductArgument(cartProductComponent.Catalog, cartProductComponent.Id),
                context.PipelineContextOptions);
            var sellableItemIdentifiers = sellableItem.GetComponent<IdentifiersComponent>();
            return sellableItemIdentifiers.SKU;
        }

        protected string DeterimeShippingCode(CommerceContext context, CommerceEntity cartOrOrder)
        {
            String shippingCode = "SH";
            var fulfillmentComponent = cartOrOrder.GetComponent<PhysicalFulfillmentComponent>();
            //fulfillmentComponent.FulfillmentMethod.EntityTargetUniqueId;
            //var shippingOption = await GetFullfilmentMethod(context.PipelineContext, fulfillmentComponent);
            //if (shippingOption != null && !String.IsNullOrEmpty(shippingOption.TaxCode))
            //{
            //    shippingCode = shippingOption.TaxCode;
            //}

            return shippingCode;
        }
        
        //protected async Task<ICustomFullfilmentMethod> GetFullfilmentMethod(CommercePipelineExecutionContext context, PhysicalFulfillmentComponent fullFillMentMethodComponent)
        //{
        //    String fullFilementMethodId = fullFillMentMethodComponent.FulfillmentMethod?.EntityTarget;
        //    ICustomFullfilmentMethod fullFilementMethod = null;
        //    if (!String.IsNullOrEmpty(fullFilementMethodId))
        //    {
        //        fullFilementMethod = (await this._getFulfillmentMethodPipeline.Run(new ItemModelArgument(fullFilementMethodId), context)) as ICustomFullfilmentMethod;
        //    }

        //    if (fullFilementMethod == null)
        //    {
        //        context.Logger.LogDebug($"{nameof(CalculateCartTaxTotalCommand)} - FullFilmentMethod not found {fullFilementMethodId}");
        //        return null;
        //    }

        //    return fullFilementMethod;
        //}
    }
}
