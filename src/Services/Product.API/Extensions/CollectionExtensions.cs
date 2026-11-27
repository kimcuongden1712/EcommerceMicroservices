namespace Product.API.Extensions
{
    public static class CollectionExtensions
    {
        public static bool AddDistinct<T> (this ICollection<T> collection, T value)
        {
            if (collection.Contains(value))
                return false;
            collection.Add(value);
            return true;
        }
    }
}
