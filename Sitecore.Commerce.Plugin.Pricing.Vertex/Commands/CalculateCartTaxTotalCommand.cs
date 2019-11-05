using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sitecore.Commerce.Core;
using Sitecore.Commerce.Plugin.Carts;
using Sitecore.Commerce.Plugin.Catalog;
using Sitecore.Commerce.Plugin.Fulfillment;
using Sitecore.Commerce.Plugin.Pricing.Vertex.CalculateTaxService;
using Sitecore.Commerce.Plugin.Pricing.Vertex.Extensions;
using Sitecore.Commerce.Plugin.Pricing.Vertex.Policies;
using Sitecore.Commerce.Plugin.Shops;
using Product = Sitecore.Commerce.Plugin.Pricing.Vertex.CalculateTaxService.Product;

namespace Sitecore.Commerce.Plugin.Pricing.Vertex.Commands
{
    public class CalculateCartTaxTotalCommand : BaseVertexCommerceCommand
    {
        private readonly IPersistEntityPipeline _persistEntityPipeline;

        public CalculateCartTaxTotalCommand(IServiceProvider serviceProvider, IGetShopPipeline getShopPipeline, IPersistEntityPipeline persistEntityPipeline, ILocalizableTermPipeline localizableTermPipeline, IGetSellableItemPipeline getSellableItemPipeline) :
            base(serviceProvider, localizableTermPipeline, getShopPipeline, getSellableItemPipeline)
        {
            _persistEntityPipeline = persistEntityPipeline;
        }

        public async Task<decimal> Process(CommerceContext context, Cart cart)
        {
            using (CommandActivity.Start(context, this))
            {
                var totalTax = 0.0M;
                var vertexClientPolicy = context.GetPolicy<VertexClientPolicy>();

                var shippingCode = DeterimeShippingCode(context, cart);

                var quotationRequest = await CreateQuotationRequest(context, cart, shippingCode);
                if (quotationRequest == null)
                    return 0.0M;

                context.Logger.LogInformation("CalculateCartTaxTotalCommand - Requesting calculateTax");
                //There is only one Endpoint Address and it is handled by the Vertex service itself, we can't set it
                using (CalculateTaxWS80Client client = new CalculateTaxWS80Client())
                {
                    try
                    {
                        var result = await client.calculateTax80Async(new calculateTaxRequest(new VertexEnvelope
                        {
                            Login = new LoginType
                            {
                                UserName = vertexClientPolicy.UserName,
                                Password = vertexClientPolicy.Password
                            },
                            Item = quotationRequest
                        }));

                        var response = (QuotationResponseType)result.VertexEnvelope.Item;
                        if (response != null)
                        {
                            totalTax = response.TotalTax;
                        }
                    }
                    catch (FaultException fault)
                    {
                        context.Logger.LogInformation($"{nameof(CalculateCartTaxTotalCommand)} - retreived fault {fault.Message}");
                        return 0.0M;

                    }
                    catch (Exception e)
                    {
                        context.Logger.LogError($"{nameof(CalculateCartTaxTotalCommand)} - retreived exception {e.Message}");
                        return 0.0M;

                    }
                    finally
                    {
                        await client.CloseAsync();
                        await _persistEntityPipeline.Run(new PersistEntityArgument(cart), context.PipelineContextOptions);
                    }
                }

                return totalTax;
            }
        }

        private async Task<QuotationRequestType> CreateQuotationRequest(CommerceContext context, Cart cart, string shippingCode = "SH")
        {
            var vertexClientPolicy = context.GetPolicy<VertexClientPolicy>();
            var result = new QuotationRequestType();
            var fulfillmentComponent = cart.GetComponent<PhysicalFulfillmentComponent>();
            if (fulfillmentComponent == null || fulfillmentComponent.ShippingParty == null)
                return null;
            Party shippingParty = fulfillmentComponent.ShippingParty;

            var sellerLocation = new LocationType
            {
                StreetAddress1 = vertexClientPolicy.StreetAddress1,
                StreetAddress2 = vertexClientPolicy.StreetAddress2,
                City = vertexClientPolicy.City,
                MainDivision = vertexClientPolicy.MainDivision, //This is state or province
                SubDivision = vertexClientPolicy.SubDivision,
                PostalCode = vertexClientPolicy.PostalCode,
                Country = vertexClientPolicy.Country //This is country code
            };
            var seller = new SellerType
            {
                Company = vertexClientPolicy.CompanyCode, //Company Code should come from policy?
                PhysicalOrigin = sellerLocation
            };

            var customerCode = new CustomerCodeType
            {
                classCode = cart.Id.RemoveIdPrefix<Cart>()
            };
            var customer = new CustomerType
            {
                CustomerCode = customerCode
            };

            var buyerLocation = new LocationType
            {
                StreetAddress1 = shippingParty.Address1,
                StreetAddress2 = shippingParty.Address2,
                City = shippingParty.City,
                MainDivision = shippingParty.StateCode,
                PostalCode = shippingParty.ZipPostalCode,
                Country = shippingParty.CountryCode
            };

            var currency = new CurrencyType
            {
                isoCurrencyCodeAlpha = cart.Totals.GrandTotal.CurrencyCode
            };

            var taxDate = DateTime.Now;
            var lineItems = new List<LineItemQSIType>();
            foreach (var line in cart.Lines)
            {
                var lineItemQSIType = new LineItemQSIType
                {
                    Seller = seller,
                    Product = new Product
                    {
                        Value = await GetProductSKU(context, line.GetComponent<CartProductComponent>())
                    },
                    Quantity = new MeasureType
                    {
                        Value = line.Quantity
                    },
                    Cost = line.UnitListPrice.Amount,
                    CostSpecified = true,
                    UnitPrice = line.UnitListPrice.Amount,
                    UnitPriceSpecified = true,
                    taxDate = taxDate,
                    taxDateSpecified = true
                };
                lineItems.Add(lineItemQSIType);
            };

            //Add shipping fee
            var adjustment = cart.Adjustments.FirstOrDefault(a =>
                a.Name.Equals("FulfillmentFee", StringComparison.InvariantCultureIgnoreCase));
            //Determine shipping cost, but don't set shipping cost if there is any FreeShipping adjustment on the cart
            var shippingCost = cart.Adjustments.Any(a => a.IsFreeShipping(context.PipelineContext)) ? 0.0M :
                (adjustment?.IsTaxable) ?? false ? adjustment.Adjustment.Amount : 0.0M;


            lineItems.Add(new LineItemQSIType
            {
                Seller = seller,
                Product = new Product { productClass = shippingCode },
                Quantity = new MeasureType { Value = 1 },
                Cost = shippingCost,
                CostSpecified = true,
                UnitPrice = shippingCost,
                UnitPriceSpecified = true,
                taxDate = taxDate
            });

            decimal discountAmount = cart.GetTotalOtherAdjustments(context.PipelineContext);
            if (discountAmount > 0)
            {
                result.Discount = new Discount()
                {
                    ItemElementName = CalculateTaxService.ItemChoiceType.DiscountAmount,
                    Item = discountAmount
                };
            }

            result.Customer = customer;
            result.Customer.Destination = buyerLocation;
            result.documentDate = DateTime.Now;
            result.transactionType = SaleTransactionType.SALE;
            result.Currency = currency;
            result.LineItem = lineItems.ToArray();

            return result;
        }
    }
}
