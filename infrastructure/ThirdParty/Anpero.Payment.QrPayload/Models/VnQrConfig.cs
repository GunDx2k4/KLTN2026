using Payment.QrPayload.Enums;
using Payment.QrPayload.Helpers;
using Payment.QrPayload.Interfaces;
using System.Text;

namespace Payment.QrPayload.Models
{
    public class VnQrConfig : IQrConfig
    {
        public string BankId { get; set; }
        public string BankAccount { get; set; }
        public string Message { get; set; }
        public string Amount { get; set; }
        
        public CurrencyCode Currency { get; set; } = CurrencyCode.VND;
        public PaymentService ServiceCode { get; set; } = PaymentService.Napas247_Account;
        public CountryCode CountryCode { get; set; } = CountryCode.VN;
        public PaymentProvider Guid { get; set; } = PaymentProvider.Napas;

        private static string FormatLength(string str)
        {
            return str.Length < 10 ? $"0{str.Length}" : str.Length.ToString();
        }

        private string GetMerchantAccountInformation()
        {
            string acquierLength = FormatLength(BankId);
            string acquier = $"00{acquierLength}{BankId}";
            
            string consumerLength = FormatLength(BankAccount);
            string consumer = $"01{consumerLength}{BankAccount}";
            
            string beneficiaryContent = $"{acquier}{consumer}";
            string beneficiaryLen = FormatLength(beneficiaryContent);
            string beneficiaryTagFull = $"01{beneficiaryLen}{beneficiaryContent}";
            
            string guidVal = QrEnumHelper.GetDescription(Guid);
            string serviceVal = QrEnumHelper.GetDescription(ServiceCode);
            
            string fullContent = $"{guidVal}{beneficiaryTagFull}02{FormatLength(serviceVal)}{serviceVal}";
            string fullLen = FormatLength(fullContent);
            
            return $"38{fullLen}{fullContent}";
        }
        
        private string GetTransactionAmount()
        {
            if (string.IsNullOrEmpty(Amount)) return string.Empty;
            string length = FormatLength(Amount);
            return $"54{length}{Amount}";
        }
        
        private string GetAdditionalDataFieldTemplate()
        {
             if (string.IsNullOrEmpty(Message)) return string.Empty;
             
             string content = Message;
             if (content.Length > 99) content = content.Substring(0, 98);
             
             string contentLength = FormatLength(content);
             string tag08 = $"08{contentLength}{content}";
             
             string mainLen = FormatLength(tag08);
             return $"62{mainLen}{tag08}";
        }

        public string GetPayload()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("000201"); // Format
            sb.Append("010212"); // Initiation Method (Dynamic)
            
            sb.Append(GetMerchantAccountInformation());
            
            string currencyVal = QrEnumHelper.GetDescription(Currency);
            sb.Append($"5303{currencyVal}"); // 53 Currency
            
            sb.Append(GetTransactionAmount()); // 54 Amount
            
            string countryVal = QrEnumHelper.GetDescription(CountryCode);
            sb.Append($"5802{countryVal}"); // 58 Country
            
            sb.Append(GetAdditionalDataFieldTemplate()); // 62 Additional Data
            
            return sb.ToString();
        }
    }
}
