using System.Threading.Tasks;
using Sitecore.Commerce.Core;
using Sitecore.Commerce.Plugin.Orders;
using Sitecore.Commerce.Plugin.Pricing.Vertex.Commands;
using Sitecore.Framework.Pipelines;

namespace Sitecore.Commerce.Plugin.Pricing.Vertex.Pipelines.ReleasedOrdersMinion.Blocks
{
    public class CreateVertexInvoiceRequest : PipelineBlock<Order, Order, CommercePipelineExecutionContext>
    {
        private readonly CreateInvoiceRequestCommand _command;

        public CreateVertexInvoiceRequest(CreateInvoiceRequestCommand command)
        {
            _command = command;
        }

        public override async Task<Order> Run(Order arg, CommercePipelineExecutionContext context)
        {
            await _command.Process(context.CommerceContext, arg);
            return arg;
        }
    }
}
