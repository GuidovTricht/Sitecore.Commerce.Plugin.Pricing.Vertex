using System;
using System.Linq;
using System.Threading.Tasks;
using Sitecore.Commerce.Core;
using Sitecore.Commerce.Plugin.Carts;
using Sitecore.Commerce.Plugin.Fulfillment;
using Sitecore.Commerce.Plugin.Pricing.Vertex.Commands;
using Sitecore.Commerce.Plugin.Shops;
using Sitecore.Framework.Conditions;
using Sitecore.Framework.Pipelines;

namespace Sitecore.Commerce.Plugin.Pricing.Vertex.Pipelines.CalculateCart.Blocks
{
    public class CalculateCartTaxBlock : PipelineBlock<Cart, Cart, CommercePipelineExecutionContext>
    {
        private readonly IGetShopPipeline _getShopPipeline;
        private readonly CalculateCartTaxTotalCommand _command;

        public CalculateCartTaxBlock(CalculateCartTaxTotalCommand command, IGetShopPipeline getShopPipeline)
        {
            _command = command;
            _getShopPipeline = getShopPipeline;
        }

        public override async Task<Cart> Run(Cart arg, CommercePipelineExecutionContext context)
        {
            Condition.Requires(arg).IsNotNull($"{this.Name as object}: The cart can not be null");
            if (!arg.HasComponent<FulfillmentComponent>()) return arg;

            var shop = await _getShopPipeline.Run(arg.ShopName, context);

            if (!arg.Lines.Any())
            {
                arg.Adjustments.Where(a =>
                {
                    if (!string.IsNullOrEmpty(a.Name) && a.Name.Equals(Constants.Tax.TaxFee, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(a.AdjustmentType))
                        return a.AdjustmentType.Equals(context.GetPolicy<KnownCartAdjustmentTypesPolicy>().Tax, StringComparison.OrdinalIgnoreCase);
                    return false;
                }).ToList().ForEach(a => arg.Adjustments.Remove(a));
                return arg;
            }
            if (arg.GetComponent<FulfillmentComponent>() is SplitFulfillmentComponent)
                return arg;

            string currencyCode = arg.Totals.GrandTotal.CurrencyCode;

            var amount = await _command.Process(context.CommerceContext, arg);
            if (amount > Decimal.Zero)
            {
                CartLevelAwardedAdjustment awardedAdjustment =
                    new CartLevelAwardedAdjustment
                    {
                        Name = Constants.Tax.TaxFee,
                        DisplayName = Constants.Tax.TaxFee,
                        Adjustment = new Money(currencyCode, amount),
                        AdjustmentType = context.GetPolicy<KnownCartAdjustmentTypesPolicy>().Tax,
                        AwardingBlock = this.Name,
                        IsTaxable = false
                    };
                arg.Adjustments.Add(awardedAdjustment);
            }
            return arg;
        }
    }
}
