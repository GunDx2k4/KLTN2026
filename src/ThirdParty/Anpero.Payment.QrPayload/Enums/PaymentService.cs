
using System.ComponentModel;

namespace Payment.QrPayload.Enums
{
    public enum PaymentService
    {
        /// <summary>
        /// 24/7 money transfer to Account (Default for Napas)
        /// </summary>
        [Description("QRIBFTTA")]
        Napas247_Account,

        /// <summary>
        /// 24/7 money transfer to Card
        /// </summary>
        [Description("0208QRIBFTTC")]
        Napas247_Card,

        /// <summary>
        /// PromptPay E-Wallet ID (Tag 29)
        /// </summary>
        [Description("29")] 
        PromptPay_EWallet,

        /// <summary>
        /// PromptPay Bill Payment (Tag 30)
        /// </summary>
        [Description("30")]
        PromptPay_BillPayment,

        /// <summary>
        /// PayNow UEN (B2B) - Proxy Type 2
        /// </summary>
        [Description("2")]
        PayNow_UEN,

        /// <summary>
        /// PayNow NRIC - Proxy Type 1
        /// </summary>
        [Description("1")]
        PayNow_NRIC,

        /// <summary>
        /// PayNow Mobile Number - Proxy Type 0
        /// </summary>
        [Description("0")]
        PayNow_Mobile,

        /// <summary>
        /// UPI Collect Request (Common VPA Tag)
        /// </summary>
        [Description("UPI")]
        UPI_Collect,

        /// <summary>
        /// Pix Key (Dictated by GUI, key is dynamic value) 
        /// Use empty for now as Pix doesn't use a fixed Service Code string
        /// </summary>
        [Description("")]
        Pix_Key,
        
        /// <summary>
        /// AliPay User (Tag 26-51 dynamic)
        /// </summary>
        [Description("ALIPAY")]
        AliPay_User,

        /// <summary>
        /// WeChat Pay User (Tag 26-51 dynamic)
        /// </summary>
        [Description("TENPAY")]
        WeChatPay_User,

        /// <summary>
        /// No service code tag
        /// </summary>
        [Description("")]
        None
    }
}
