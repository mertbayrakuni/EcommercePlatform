namespace OrderService.Common;

public static class Paging
{
    public static int ClampPage(int page) => page < 1 ? 1 : page;

    public static int ClampPageSize(int pageSize, int defaultSize = 10, int maxSize = 200)
    {
        if (pageSize <= 0) return defaultSize;
        if (pageSize > maxSize) return maxSize;
        return pageSize;
    }

    public static int TotalPages(int totalCount, int pageSize)
        => pageSize <= 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
}