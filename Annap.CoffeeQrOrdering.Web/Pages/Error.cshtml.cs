using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Annap.CoffeeQrOrdering.Web.Pages;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public class ErrorModel : PageModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    public int StatusCodeValue { get; set; } = 500;

    public string Title { get; set; } = "Đã xảy ra lỗi";

    public string Lede { get; set; } =
        "Chúng tôi không thể hoàn tất yêu cầu của bạn. Vui lòng thử lại hoặc quay lại thực đơn.";

    public void OnGet(int? statusCode = null)
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        StatusCodeValue = statusCode is > 0 ? statusCode.Value : Response.StatusCode;
        if (StatusCodeValue is < 400 or > 599)
            StatusCodeValue = 500;

        Response.StatusCode = StatusCodeValue;

        (Title, Lede) = StatusCodeValue switch
        {
            404 => (
                "Không tìm thấy trang",
                "Liên kết có thể đã đổi hoặc bàn chưa được cấu hình. Quay lại menu để tiếp tục."),
            403 => (
                "Không có quyền truy cập",
                "Bạn không thể mở trang này. Nếu bạn là nhân viên, hãy đăng nhập lại."),
            503 => (
                "Tạm bảo trì",
                "Quán đang bảo trì hệ thống trong giây lát. Vui lòng thử lại sau."),
            _ => (
                "Đã xảy ra lỗi",
                "Chúng tôi không thể hoàn tất yêu cầu của bạn. Vui lòng thử lại hoặc quay lại thực đơn.")
        };
    }
}
