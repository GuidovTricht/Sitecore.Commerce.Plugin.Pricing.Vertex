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
using Sitecore.Commerce.Plugin.Orders;
using Sitecore.Commerce.Plugin.Pricing.Vertex.CalculateTaxService;
using Sitecore.Commerce.Plugin.Pricing.Vertex.Extensions;
using Sitecore.Commerce.Plugin.Pricing.Vertex.Policies;
using Sitecore.Commerce.Plugin.Shops;

namespace Sitecore.Commerce.Plugin.Pricing.Vertex.Commands
{
    public class CreateInvoiceRequestCommand : BaseVertexCommerceCommand
    {
        public CreateInvoiceRequestCommand(IServiceProvider serviceProvider, IGetShopPipeline getShopPipeline, ILocalizableTermPipeline localizableTermPipeline, IGetSellableItemPipeline getSellableItemPipeline) :
            base(serviceProvider, localizableTermPipeline, getShopPipeline, getSellableItemPipeline)
        {
        }

        public async Task Process(CommerceContext context, Order order)
        {
            using (CommandActivity.Start(context, this))
            {
                var shippingCode = DeterimeShippingCode(context, order);

                var invoiceRequest = await CreateInvoiceRequest(context, order, shippingCode);
                if (invoiceRequest == null)
                    return;

                var vertexClientPolicy = context.GetPolicy<VertexClientPolicy>();

                context.Logger.LogInformation($"{nameof(CreateInvoiceRequestCommand)} - Creating Invoice");
                //There is only one Endpoint Address and it is handled by the Vertex service itself, we can't set it
                using (CalculateTaxWS80Client client = new CalculateTaxWS80Client())
                {
                    var messagesComponent = order.GetComponent<MessagesComponent>();
                    try
                    {
                        var result = await client.calculateTax80Async(new calculateTaxRequest(new VertexEnvelope
                        {
                            Login = new LoginType
                            {
                                UserName = vertexClientPolicy.UserName,
                                Password = vertexClientPolicy.Password
                            },
                            Item = invoiceRequest
                        }));

                        var response = (InvoiceResponseType)result.VertexEnvelope.Item;
                    }
                    catch (FaultException fault)
                    {
                        context.Logger.LogInformation($"{nameof(CreateInvoiceRequestCommand)} - retreived fault {fault.Message}");
                    }
                    catch (Exception e)
                    {
                        context.Logger.LogError($"{nameof(CreateInvoiceRequestCommand)} - retreived exception {e.Message}");
                    }
                    finally
                    {
                        await client.CloseAsync();
                    }
                }

            }
        }

        private async Task<InvoiceRequestType> CreateInvoiceRequest(CommerceContext context, Order order, string shippingCode = "SH")
        {
            var vertexClientPolicy = context.GetPolicy<VertexClientPolicy>();
            var result = new InvoiceRequestType();
            var fulfillmentComponent = order.GetComponent<PhysicalFulfillmentComponent>();
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
                classCode = order.Id.RemoveIdPrefix<Order>()//vertexClientPolicy.ClassCode
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
                isoCurrencyCodeAlpha = order.Totals.GrandTotal.CurrencyCode
            };

            var taxDate = DateTime.Now;
            var lineItems = new List<LineItemISIType>();
            foreach (var line in order.Lines)
            {
                var lineItemISIType = new LineItemISIType
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

                lineItems.Add(lineItemISIType);
            };

            //Add shipping fee
            var adjustment = order.Adjustments.FirstOrDefault(a =>
                a.Name.Equals("FulfillmentFee", StringComparison.InvariantCultureIgnoreCase));
            //Determine shipping cost, but don't set shipping cost if there is any CartFreeShippingAction adjustment on the cart
            var shippingCost = order.Adjustments.Any(a => a.IsFreeShipping(context.PipelineContext)) ? 0.0M :
                (adjustment?.IsTaxable) ?? false ? adjustment.Adjustment.Amount : 0.0M;

            lineItems.Add(new LineItemISIType
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

            decimal discountAmount = order.GetTotalOtherAdjustments(context.PipelineContext);
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
            result.documentDate = DateTime.UtcNow;
            result.transactionType = SaleTransactionType.SALE;
            result.Currency = currency;
            result.LineItem = lineItems.ToArray();
            result.postingDate = DateTime.UtcNow;
            result.postingDateSpecified = true;
            return result;
        }
    }
}
