using Payment.QrPayload.Helpers;
using Payment.QrPayload.Interfaces;
using Payment.QrPayload.Models;
using Payment.QrPayload.Enums;

namespace Payment.QrPayload
{
    public static class QrPayloadGenerator
    {
        public static QrPayloadResult Generate(IQrConfig config)
        {
            try
            {
                string rawPayload = config.GetPayload();
                string payloadWithCrcId = rawPayload + "6304";
                
                ushort crc = Crc16.ComputeChecksum(payloadWithCrcId);
                string crcHex = crc.ToString("X4");
                
                string finalPayload = payloadWithCrcId + crcHex;
                
                return QrPayloadResult.Success(finalPayload);
            }
            catch (System.Exception ex)
            {
                return QrPayloadResult.Fail(ex.Message);
            }
        }

        /// <summary>
        /// Compatibility method to match the request signature.
        /// </summary>
        public static QrPayloadResult GeneratePayload(string bankAccount, string bankId, string message, string amount, 
            CurrencyCode currency = CurrencyCode.VND, 
            PaymentService service = PaymentService.Napas247_Account, 
            CountryCode countryCode = CountryCode.VN, 
            PaymentProvider guid = PaymentProvider.Napas)
        {
             var config = new VnQrConfig
             {
                 BankAccount = bankAccount,
                 BankId = bankId,
                 Message = message,
                 Amount = amount,
                 Currency = currency,
                 ServiceCode = service,
                 CountryCode = countryCode,
                 Guid = guid
             };
             
             return Generate(config);
        }
    }
}
