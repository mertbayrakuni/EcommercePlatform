namespace CatalogService.Common;

public static class Paging
{
    public static int ClampPage(int page) => page < 1 ? 1 : page;

    public static int ClampPageSize(int pageSize, int defaultSize = 10, int maxSize = 200)
    {
        if (pageSize < 1) return defaultSize;
        if (pageSize > maxSize) return maxSize;
        return pageSize;
    }
}