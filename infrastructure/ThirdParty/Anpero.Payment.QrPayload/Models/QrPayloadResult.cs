namespace Payment.QrPayload.Models
{
    public class QrPayloadResult
    {
        public bool IsSuccess { get; set; }
        public string Content { get; set; }
        public string Message { get; set; }

        public QrPayloadResult()
        {
            IsSuccess = false;
            Content = string.Empty;
            Message = string.Empty;
        }

        public static QrPayloadResult Success(string content)
        {
            return new QrPayloadResult
            {
                IsSuccess = true,
                Content = content,
                Message = "Success"
            };
        }

        public static QrPayloadResult Fail(string message)
        {
            return new QrPayloadResult
            {
                IsSuccess = false,
                Message = message
            };
        }
    }
}
