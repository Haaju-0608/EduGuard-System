using EduGuardProject.DTOs.Response;
using EduGuardProject.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace EduGuardProject.Controllers;

public abstract class AcademicApiControllerBase : ControllerBase
{
    protected IActionResult OkPaged<T>(IEnumerable<T> items, int page, int pageSize, int total, string message, string? fields)
    {
        var data = FieldSelectionHelper.ApplyFields(items, fields);
        IEnumerable<object> payload = data switch
        {
            IEnumerable<object> objects => objects,
            null => [],
            _ => [data]
        };

        return Ok(ApiPagedResponse<object>.OnPagedSuccess(payload, page, pageSize, total, message));
    }

    protected IActionResult OkSingle<T>(T data, string message, string? fields = null)
    {
        var shaped = FieldSelectionHelper.ApplyFields(data, fields);
        return Ok(ApiResponse<object>.OnSuccess(shaped!, message));
    }

    protected IActionResult CreatedSingle<T>(T data, string message, string? fields = null)
    {
        var shaped = FieldSelectionHelper.ApplyFields(data, fields);
        return StatusCode(201, ApiResponse<object>.OnSuccess(shaped!, message));
    }

    protected IActionResult HandleException(Exception ex)
    {
        return ex switch
        {
            UnauthorizedAccessException => StatusCode(403, ApiResponse<object>.OnFail(ex.Message)),
            InvalidOperationException => BadRequest(ApiResponse<object>.OnFail(ex.Message)),
            _ => BadRequest(ApiResponse<object>.OnFail($"System error: {ex.Message}"))
        };
    }

    protected IActionResult BadPagedRequest(string _) =>
        BadRequest(ApiResponse<object>.OnFail("Page must be greater than 0 and pageSize must be between 1 and 100."));

    protected bool ValidatePaging(int page, int pageSize) => page >= 1 && pageSize is >= 1 and <= 100;
}
