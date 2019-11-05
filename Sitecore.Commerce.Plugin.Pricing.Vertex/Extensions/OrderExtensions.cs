using System.Linq;
using Sitecore.Commerce.Core;
using Sitecore.Commerce.Plugin.Carts;
using Sitecore.Commerce.Plugin.Orders;

namespace Sitecore.Commerce.Plugin.Pricing.Vertex.Extensions
{
    public static class OrderExtensions
    {
        public static decimal GetTotalOtherAdjustments(this Order order, CommercePipelineExecutionContext context)
        {
            return order.Adjustments
                .Where(it =>
                    it.AdjustmentType == context.GetPolicy<KnownCartAdjustmentTypesPolicy>().Discount &&
                    !it.IsFreeShipping(context) &&
                    it.IncludeInGrandTotal)
                .Sum(it => it.Adjustment.Amount);
        }
    }
}
