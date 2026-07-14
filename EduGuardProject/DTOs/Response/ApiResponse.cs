namespace EduGuardProject.DTOs.Response
{
    //File nay để đồng bộ json á nha 
    // 1. Khung phản hồi chuẩn cho tất cả API nè
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public object? Errors { get; set; }

        // Hàm tạo nhanh phản hồi Thành Công 
        public static ApiResponse<T> OnSuccess(T data, string message = "Request processed successfully")
        {
            return new ApiResponse<T> { Success = true, Message = message, Data = data, Errors = null };
        }

        // Hàm tạo nhanh phản hồi Thất Bại
        public static ApiResponse<T> OnFail(string message, object? errors = null)
        {
            return new ApiResponse<T> { Success = false, Message = message, Data = default, Errors = errors };
        }
    }

    // 2. Cấu trúc Metadata Phân trang 
    public class PaginationMetadata
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
    }

    // 3. Khung phản hồi chuẩn dành riêng cho các API trả về Danh sách có phân trang
    public class ApiPagedResponse<T> : ApiResponse<IEnumerable<T>>
    {
        public PaginationMetadata Pagination { get; set; } = new PaginationMetadata();

        public static ApiPagedResponse<T> OnPagedSuccess(IEnumerable<T> data, int page, int pageSize, int totalItems, string message = "Request processed successfully")
        {
            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            return new ApiPagedResponse<T>
            {
                Success = true,
                Message = message,
                Data = data,
                Errors = null,
                Pagination = new PaginationMetadata
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalItems = totalItems,
                    TotalPages = totalPages
                }
            };
        }
    }
}
