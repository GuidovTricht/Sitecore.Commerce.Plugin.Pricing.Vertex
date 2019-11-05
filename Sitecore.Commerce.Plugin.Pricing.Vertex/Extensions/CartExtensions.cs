using System.Linq;
using Sitecore.Commerce.Core;
using Sitecore.Commerce.Plugin.Carts;

namespace Sitecore.Commerce.Plugin.Pricing.Vertex.Extensions
{
    public static class CartExtensions
    {
        public static decimal GetTotalOtherAdjustments(this Cart cart, CommercePipelineExecutionContext context)
        {
            return cart.Adjustments
                .Where(it =>
                    it.AdjustmentType == context.GetPolicy<KnownCartAdjustmentTypesPolicy>().Discount &&
                    !it.IsFreeShipping(context) &&
                    it.IncludeInGrandTotal)
                .Sum(it => it.Adjustment.Amount);
        }
    }
}
