using Sitecore.Commerce.Core;

namespace Sitecore.Commerce.Plugin.Pricing.Vertex.Policies
{
    public class VertexClientPolicy : Policy
    {
        public VertexClientPolicy()
        {
        }

        public string CompanyCode { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string ClassCode { get; set; }

        //Seller location
        public string StreetAddress1 { get; set; }
        public string StreetAddress2 { get; set; }
        public string City { get; set; }
        public string MainDivision { get; set; } //State or province
        public string SubDivision { get; set; }
        public string PostalCode { get; set; }
        public string Country { get; set; }
    }
}
