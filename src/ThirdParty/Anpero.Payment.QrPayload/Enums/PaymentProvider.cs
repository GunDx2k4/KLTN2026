
using System.ComponentModel;

namespace Payment.QrPayload.Enums
{
    public enum PaymentProvider
    {
        [Description("0010A000000727")]
        Napas,

        [Description("0010A000000003")]
        Visa,

        [Description("0010A000000004")]
        Mastercard,

        [Description("0010A000000333")]
        UnionPay,

        [Description("0010A000000065")]
        JCB,

        [Description("0010A000000025")]
        AmericanExpress,
        
        [Description("0010A000000152")]
        Discover,

        /// <summary>
        /// Thailand PromptPay (GUID: A000000677)
        /// </summary>
        [Description("0010A000000677")]
        PromptPay,

        /// <summary>
        /// Singapore PayNow (GUID: SG.PAYNOW)
        /// </summary>
        [Description("0009SG.PAYNOW")]
        PayNow,

        /// <summary>
        /// Malaysia DuitNow (GUID: A000000762)
        /// </summary>
        [Description("0010A000000762")]
        DuitNow,

        /// <summary>
        /// Indonesia QRIS (GUID: ID.CO.QRIS.WWW)
        /// </summary>
        [Description("0014ID.CO.QRIS.WWW")]
        QRIS,

        /// <summary>
        /// Brazil Pix (GUID: BR.GOV.BCB.PIX)
        /// </summary>
        [Description("0014BR.GOV.BCB.PIX")]
        Pix,

        /// <summary>
        /// AliPay (GUID: ALIPAY.COM)
        /// </summary>
        [Description("0010ALIPAY.COM")]
        AliPay,

        /// <summary>
        /// WeChat Pay / Tenpay (GUID: TENPAY.COM)
        /// </summary>
        [Description("0010TENPAY.COM")]
        WeChatPay
    }
}
