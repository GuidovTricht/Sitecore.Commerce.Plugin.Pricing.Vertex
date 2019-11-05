using Sitecore.Commerce.Core;
using Sitecore.Commerce.Plugin.Carts;
using Sitecore.Commerce.Plugin.Fulfillment;

namespace Sitecore.Commerce.Plugin.Pricing.Vertex.Extensions
{
    public static class AwardedAdjustmentExtensions
    {
        public static bool IsFreeShipping(this AwardedAdjustment it, CommercePipelineExecutionContext context)
        {
            return it.AdjustmentType == context.GetPolicy<KnownCartAdjustmentTypesPolicy>().Discount && it.AwardingBlock == nameof(CartFreeShippingAction);
        }
    }
}
