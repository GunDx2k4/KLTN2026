using System.Text;

namespace Payment.QrPayload.Helpers
{
    public static class Crc16
    {
        private static readonly ushort[] CrcTable = new ushort[256];

        static Crc16()
        {
            const ushort polynomial = 0x1021;
            for (ushort i = 0; i < CrcTable.Length; ++i)
            {
                ushort crc = 0;
                ushort c = (ushort)(i << 8);
                for (byte j = 0; j < 8; ++j)
                {
                    if (((crc ^ c) & 0x8000) != 0)
                    {
                        crc = (ushort)((crc << 1) ^ polynomial);
                    }
                    else
                    {
                        crc <<= 1;
                    }
                    c <<= 1;
                }
                CrcTable[i] = crc;
            }
        }

        public static ushort ComputeChecksum(string input)
        {
            ushort crc = 0xFFFF;
            foreach (byte b in Encoding.ASCII.GetBytes(input))
            {
                byte tableIndex = (byte)(((crc >> 8) ^ b) & 0xFF);
                crc = (ushort)((crc << 8) ^ CrcTable[tableIndex]);
            }
            return crc;
        }

        public static bool ValidateQrCode(string qrData)
        {
            if (qrData.Length < 4) return false;
            string data = qrData.Substring(0, qrData.Length - 4);
            string crcString = qrData.Substring(qrData.Length - 4);
            
            try 
            {
                ushort expectedCrc = System.Convert.ToUInt16(crcString, 16);
                ushort computedCrc = ComputeChecksum(data);
                return expectedCrc == computedCrc;
            }
            catch
            {
                return false;
            }
        }
    }
}
