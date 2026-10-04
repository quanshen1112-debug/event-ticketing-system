using QRCoder;

namespace EventXpress.Services
{
    public interface IQrCodeService
    {
        string GenerateBase64(string payload);
    }

    // Additional Feature: QR code generation for each booked ticket (entry scanning)
    public class QrCodeService : IQrCodeService
    {
        public string GenerateBase64(string payload)
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
            var pngQrCode = new PngByteQRCode(qrData);
            byte[] bytes = pngQrCode.GetGraphic(10);
            return Convert.ToBase64String(bytes);
        }
    }
}
