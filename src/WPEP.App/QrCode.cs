using System.IO;
using System.Windows.Media.Imaging;
using QRCoder;

namespace WPEP.App;

/// <summary>Renders a URL to a QR image, fully in managed code (PngByteQRCode — no System.Drawing,
/// no native deps). Used for the on-phone BIOS guide link.</summary>
public static class QrCode
{
    public static BitmapImage? ForUrl(string url, int pixelsPerModule = 8)
    {
        try
        {
            using var gen = new QRCodeGenerator();
            var data = gen.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
            byte[] png = new PngByteQRCode(data).GetGraphic(pixelsPerModule);

            var bmp = new BitmapImage();
            using var ms = new MemoryStream(png);
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null; // never let a QR failure crash a row
        }
    }
}
