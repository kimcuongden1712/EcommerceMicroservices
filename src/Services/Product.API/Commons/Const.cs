using System.Text.RegularExpressions;

namespace Product.API.Commons
{
    /// <summary>
    /// Create constants for the Product API service.
    /// </summary>
    public static class Const
    {
        public const string CURRENCY = "USD";
        public const string CATALOGAUDITACTIVATED = "Activated";
        public const string CATALOGAUDITDEACTIVATED = "Deactivated";
        public const int CATEGORYENTITYTYPE = 1;
        public const int PRODUCTENTITYTYPE = 2;
        public const int PRODUCTVARIANTENTITYTYPE = 3;
        public const int MAXIMUMPRICEMINOR = 99_999_999;

        public static readonly Regex SLUGPATTERN = new("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled);
        public static readonly Regex CODEPATTERN = new("^[A-Z0-9_-]+$", RegexOptions.Compiled);
        public static readonly Regex SKUPATTERN = new("^[A-Z0-9_-]{1,64}$", RegexOptions.Compiled);
    }

    public static class CatalogPermissionPolicies
    {
        public const string View = "Catalog.Product.View";
        public const string Create = "Catalog.Product.Create";
        public const string Update = "Catalog.Product.Update";
        public const string Delete = "Catalog.Product.Delete";
    }
}
